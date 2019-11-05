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
using System.Threading;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Generators;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Modules {
    internal class CompiledPythonModule : PythonModule {
        protected IStubCache StubCache => Interpreter.ModuleResolution.StubCache;

        public CompiledPythonModule(string moduleName, ModuleType moduleType, string filePath, IPythonModule stub, bool isPersistent, bool isTypeshed, IServiceContainer services)
            : base(moduleName, filePath, moduleType, stub, isPersistent, isTypeshed, services) { }

        public override string Documentation
            => GetMember("__doc__").TryGetConstant<string>(out var s) ? s : string.Empty;

        protected virtual string[] GetScrapeArguments(IPythonInterpreter interpreter) {
            var mp = Interpreter.ModuleResolution.FindModule(FilePath);
            if (string.IsNullOrEmpty(mp.FullName)) {
                return null;
            }

            var args = new List<string>();
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

                var args = GetScrapeArguments(Interpreter);
                if (args == null) {
                    return string.Empty;
                }

                code = StubGenerator.Scrape(Interpreter, Log, this, args, CancellationToken.None);
                SaveCachedCode(code);
            }

            return code;
        }

        protected virtual void SaveCachedCode(string code) => StubCache.WriteCachedModule(FilePath, code);
    }
}
