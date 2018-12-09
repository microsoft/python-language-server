﻿// Python Tools for Visual Studio
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
using System.IO;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal class AstBuiltinPythonModule : AstScrapedPythonModule {
        public AstBuiltinPythonModule(string name, IPythonInterpreter interpreter)
            : base(name, MakeFakeFilePath(interpreter.Configuration.InterpreterPath, name), interpreter) {
        }

        private static string MakeFakeFilePath(string interpreterPath, string name) {
            if (string.IsNullOrEmpty(interpreterPath)) {
                return "python.{0}.exe".FormatInvariant(name);
            }
            var ext = Path.GetExtension(interpreterPath);
            if (ext.Length > 0) { // Typically Windows, make python.exe into python.name.exe
                return Path.ChangeExtension(interpreterPath, name) + ext;
            }
            return $"{interpreterPath}.{name}.exe"; // Fake the extension
        }

        protected override List<string> GetScrapeArguments(IPythonInterpreter interpreter)
            => !InstallPath.TryGetFile("scrape_module.py", out var sm)
                ? null
                : new List<string> { "-B", "-E", sm, "-u8", Name };
    }
}
