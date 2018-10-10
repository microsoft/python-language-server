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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public static class PythonInstallPathResolver {
        private static readonly Lazy<IPythonInstallPathResolver> _instance = new Lazy<IPythonInstallPathResolver>(() => {
            switch (Environment.OSVersion.Platform) {
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                    return new WindowsPythonInstallPathResolver();
                case PlatformID.MacOSX:
                    return new MacPythonInstallPathResolver();
                default:
                    return null;
            }
        });

        public static IPythonInstallPathResolver Instance => _instance.Value ?? throw new PlatformNotSupportedException();
    }

    public interface IPythonInstallPathResolver {
        InterpreterConfiguration GetCorePythonConfiguration(InterpreterArchitecture architecture, Version version);
        InterpreterConfiguration GetCondaPythonConfiguration(InterpreterArchitecture architecture, Version version);
        InterpreterConfiguration GetIronPythonConfiguration(bool x64);
    }
}