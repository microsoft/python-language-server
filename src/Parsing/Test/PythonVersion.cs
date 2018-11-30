// Python Tools for Visual Studio
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

using Microsoft.Python.Core.Interpreter;

namespace Microsoft.Python.Parsing.Tests {
    public class PythonVersion {
        public readonly InterpreterConfiguration Configuration;
        public readonly bool IsCPython;

        public PythonVersion(InterpreterConfiguration config, bool cPython = false) {
            Configuration = config;
            IsCPython = cPython;
        }

        public override string ToString() => Configuration.Description;
        public string LibraryPath => Configuration.LibraryPath;
        public string InterpreterPath => Configuration.InterpreterPath;
        public PythonLanguageVersion Version => Configuration.Version.ToLanguageVersion();
        public string Id => Configuration.Id;
        public bool Isx64 => Configuration.Architecture == InterpreterArchitecture.x64;
        public InterpreterArchitecture Architecture => Configuration.Architecture;
    }
}
