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

using System.IO;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents compiled module that is built into the language.
    /// </summary>
    internal sealed class CompiledBuiltinPythonModule : CompiledPythonModule {
        public CompiledBuiltinPythonModule(string moduleName, IPythonModule stub, IServiceContainer services)
            : base(moduleName, ModuleType.CompiledBuiltin, MakeFakeFilePath(moduleName, services), stub, services) { }

        protected override string[] GetScrapeArguments(IPythonInterpreter interpreter)
            => !InstallPath.TryGetFile("scrape_module.py", out var sm) ? null : new [] { "-W", "ignore", "-B", "-E", sm, "-u8", Name };

        private static string MakeFakeFilePath(string name, IServiceContainer services) {
            var interpreterPath = services.GetService<IPythonInterpreter>().Configuration.InterpreterPath;

            if (string.IsNullOrEmpty(interpreterPath)) {
                return "python.{0}.exe".FormatInvariant(name);
            }
            var ext = Path.GetExtension(interpreterPath);
            if (ext.Length > 0) { // Typically Windows, make python.exe into python.name.exe
                return Path.ChangeExtension(interpreterPath, name) + ext;
            }
            return $"{interpreterPath}.{name}.exe"; // Fake the extension
        }
    }
}
