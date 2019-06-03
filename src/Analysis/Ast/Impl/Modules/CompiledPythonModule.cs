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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules {
    internal class CompiledPythonModule : PythonModule {
        protected IStubCache StubCache => Interpreter.ModuleResolution.StubCache;

        public CompiledPythonModule(string moduleName, ModuleType moduleType, string filePath, IPythonModule stub, IServiceContainer services)
            : base(moduleName, filePath, moduleType, stub, services) { }

        public override string Documentation
            => GetMember("__doc__").TryGetConstant<string>(out var s) ? s : string.Empty;

        protected virtual string[] GetScrapeArguments(IPythonInterpreter interpreter) {
            var args = new List<string> { "-W", "ignore", "-B", "-E" };

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

            return args.ToArray();
        }

        protected override string LoadContent() {
            // Exceptions are handled in the base
            var code = StubCache.ReadCachedModule(FilePath);
            if (string.IsNullOrEmpty(code)) {
                if (!FileSystem.FileExists(Interpreter.Configuration.InterpreterPath)) {
                    return string.Empty;
                }

                code = ScrapeModule();
                SaveCachedCode(code);
            }
            return code;
        }

        protected virtual void SaveCachedCode(string code) => StubCache.WriteCachedModule(FilePath, code);

        private string ScrapeModule() {
            var args = GetScrapeArguments(Interpreter);
            if (args == null) {
                return string.Empty;
            }

            var startInfo = new ProcessStartInfo {
                FileName = Interpreter.Configuration.InterpreterPath,
                Arguments = args.AsQuotedArguments(),
                WorkingDirectory = Interpreter.Configuration.LibraryPath,
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Log?.Log(TraceEventType.Verbose, "Scrape", startInfo.FileName, startInfo.Arguments);
            var output = string.Empty;

            try {
                using (var process = new Process()) {
                    process.StartInfo = startInfo;
                    process.ErrorDataReceived += new DataReceivedEventHandler((s, e) => { });

                    process.Start();
                    process.BeginErrorReadLine();

                    output = process.StandardOutput.ReadToEnd();
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) { }

            return output;
        }
    }
}
