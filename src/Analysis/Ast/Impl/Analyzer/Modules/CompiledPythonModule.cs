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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal class CompiledPythonModule : PythonModule {
        protected IModuleCache ModuleCache => Interpreter.ModuleResolution.ModuleCache;
        public CompiledPythonModule(string moduleName, ModuleType moduleType, string filePath, IServiceContainer services)
            : base(moduleName, null, filePath, null, moduleType, DocumentCreationOptions.Analyze, services) {
        }

        public override string Documentation
            => GetMember("__doc__") is AstPythonStringLiteral m ? m.Value : string.Empty;

        public override IEnumerable<string> GetChildrenModuleNames() => Enumerable.Empty<string>();

        #region IMemberContainer
        public override IPythonType GetMember(string name) {
            Members.TryGetValue(name, out var m);
            if (m is ILazyType lm) {
                m = lm.Get();
                Members[name] = m;
            }
            return m;
        }

        public override IEnumerable<string> GetMemberNames() => Members.Keys.ToArray();
        #endregion

        protected virtual IEnumerable<string> GetScrapeArguments(IPythonInterpreter interpreter) {
            var args = new List<string> { "-B", "-E" };

            var mp = Interpreter.ModuleResolution.FindModule(FilePath);
            if (string.IsNullOrEmpty(mp.FullName)) {
                return null;
            }

            if (!InstallPath.TryGetFile("scrape_module.py", out var sm)) {
                return null;
            }

            args.Add(sm);
            args.Add("-u8");
            args.Add(mp.ModuleName);
            args.Add(mp.LibraryPath);

            return args;
        }

        protected override string LoadContent() {
            var code = ModuleCache.ReadCachedModule(FilePath);
            if (string.IsNullOrEmpty(code)) {
                if (!FileSystem.FileExists(Interpreter.Configuration.InterpreterPath)) {
                    return string.Empty;
                }
                code = ScrapeModule();
                SaveCachedCode(code);
            }
            return code;
        }

        protected virtual void SaveCachedCode(string code) => ModuleCache.WriteCachedModule(FilePath, code);

        private string ScrapeModule() {
            var args = GetScrapeArguments(Interpreter);
            if (args == null) {
                return string.Empty;
            }

            var sb = new StringBuilder();
            using (var proc = new ProcessHelper(
                Interpreter.Configuration.InterpreterPath,
                args,
                Interpreter.Configuration.LibraryPath
            )) {
                proc.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                proc.OnOutputLine = s => sb.AppendLine(s);
                proc.OnErrorLine = s => Log?.Log(TraceEventType.Error, "Scrape", s);

                Log?.Log(TraceEventType.Information, "Scrape", proc.FileName, proc.Arguments);

                proc.Start();
                var exitCode = proc.Wait(60000);

                if (exitCode == null) {
                    proc.Kill();
                    Log?.Log(TraceEventType.Error, "ScrapeTimeout", proc.FileName, proc.Arguments);
                    return string.Empty;
                }

                if (exitCode != 0) {
                    Log?.Log(TraceEventType.Error, "Scrape", "ExitCode", exitCode);
                    return string.Empty;
                }
            }

            return sb.ToString();
        }
    }
}
