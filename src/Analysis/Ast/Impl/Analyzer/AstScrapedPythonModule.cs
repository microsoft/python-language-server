// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal class AstScrapedPythonModule : PythonModuleType, IPythonModule {
        private bool _scraped;
        private ILogger Log => Interpreter.Log;

        protected ConcurrentDictionary<string, IMember> Members { get; } = new ConcurrentDictionary<string, IMember>();
        protected IModuleCache ModuleCache => Interpreter.ModuleResolution.ModuleCache;

        public AstScrapedPythonModule(string name, string filePath, IPythonInterpreter interpreter) 
            : base(name) {
            Interpreter = interpreter;
            FilePath = filePath;
        }

        public override string FilePath { get; }
        public override IPythonInterpreter Interpreter { get; }

        public override string Documentation
            => GetMember("__doc__") is AstPythonStringLiteral m ? m.Value : string.Empty;

        public IEnumerable<string> GetChildrenModuleNames() => Enumerable.Empty<string>();

        public override IMember GetMember(string name) {
            if (!_scraped) {
                NotifyImported();
            }
            Members.TryGetValue(name, out var m);
            if (m is ILazyMember lm) {
                m = lm.Get();
                Members[name] = m;
            }
            return m;
        }

        public override IEnumerable<string> GetMemberNames() {
            if (!_scraped) {
                NotifyImported();
            }
            return Members.Keys.ToArray();
        }

        public IEnumerable<string> ParseErrors { get; private set; } = Enumerable.Empty<string>();

        internal static bool KeepAst { get; set; }
        internal PythonAst Ast { get; private set; }

        protected virtual List<string> GetScrapeArguments(IPythonInterpreter interpreter) {
            var args = new List<string> { "-B", "-E" };

            var mp = Interpreter.ModuleResolution.FindModuleAsync(FilePath, CancellationToken.None).WaitAndUnwrapExceptions();
            if (string.IsNullOrEmpty(mp.FullName)) {
                return null;
            }

            if (!InstallPath.TryGetFile("scrape_module.py", out string sm)) {
                return null;
            }

            args.Add(sm);
            args.Add("-u8");
            args.Add(mp.ModuleName);
            args.Add(mp.LibraryPath);

            return args;
        }

        protected virtual PythonWalker PrepareWalker(PythonAst ast) {
#if DEBUG
            // In debug builds we let you F12 to the scraped file
            var filePath = string.IsNullOrEmpty(FilePath) ? null : ModuleCache.GetCacheFilePath(FilePath);
            var uri = string.IsNullOrEmpty(filePath) ? null : new Uri(filePath);
#else
            const string filePath = null;
            const Uri uri = null;
            const bool includeLocations = false;
#endif
            return new AstAnalysisWalker(this, ast, suppressBuiltinLookup: false);
        }

        protected virtual void PostWalk(PythonWalker walker) => (walker as AstAnalysisWalker)?.Complete();
        protected virtual Stream LoadCachedCode() => ModuleCache.ReadCachedModule(FilePath);
        protected virtual void SaveCachedCode(Stream code) => ModuleCache.WriteCachedModule(FilePath, code);

        public void NotifyImported() {
            if (_scraped) {
                return;
            }
            Debugger.NotifyOfCrossThreadDependency();

            var code = LoadCachedCode();
            bool needCache = code == null;

            _scraped = true;

            if (needCache) {
                if (!File.Exists(Interpreter.InterpreterPath)) {
                    return;
                }

                var args = GetScrapeArguments(Interpreter);
                if (args == null) {
                    return;
                }

                var ms = new MemoryStream();
                code = ms;

                using (var sw = new StreamWriter(ms, Encoding.UTF8, 4096, true))
                using (var proc = new ProcessHelper(
                    Interpreter.InterpreterPath,
                    args,
                    Interpreter.LibraryPath
                )) {
                    proc.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    proc.OnOutputLine = sw.WriteLine;
                    proc.OnErrorLine = s => Log?.Log(TraceEventType.Error, "Scrape", s);

                    Log?.Log(TraceEventType.Information, "Scrape", proc.FileName, proc.Arguments);

                    proc.Start();
                    var exitCode = proc.Wait(60000);

                    if (exitCode == null) {
                        proc.Kill();
                        Log?.Log(TraceEventType.Error, "ScrapeTimeout", proc.FileName, proc.Arguments);
                        return;
                    }
                    if (exitCode != 0) {
                        Log?.Log(TraceEventType.Error, "Scrape", "ExitCode", exitCode);
                        return;
                    }
                }

                code.Seek(0, SeekOrigin.Begin);
            }

            PythonAst ast;
            using (code) {
                var sink = new CollectingErrorSink();
                using (var sr = new StreamReader(code, Encoding.UTF8, true, 4096, true)) {
                    var parser = Parser.CreateParser(sr, Interpreter.LanguageVersion, new ParserOptions { ErrorSink = sink, StubFile = true });
                    ast = parser.ParseFile();
                }

                ParseErrors = sink.Errors.Select(e => "{0} ({1}): {2}".FormatUI(FilePath ?? "(builtins)", e.Span, e.Message)).ToArray();
                if (ParseErrors.Any()) {
                    Log?.Log(TraceEventType.Error, "Parse", FilePath ?? "(builtins)");
                    foreach (var e in ParseErrors) {
                        Log?.Log(TraceEventType.Error, "Parse", e);
                    }
                }

                if (needCache) {
                    // We know we created the stream, so it's safe to seek here
                    code.Seek(0, SeekOrigin.Begin);
                    SaveCachedCode(code);
                }
            }

            if (KeepAst) {
                Ast = ast;
            }

            var walker = PrepareWalker(ast);
            ast.Walk(walker);
            PostWalk(walker);
        }
    }
}
