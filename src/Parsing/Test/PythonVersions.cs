﻿// Copyright(c) Microsoft Corporation
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
using System.Linq;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Python.Parsing.Tests {
    public static class PythonVersions {
        public static readonly InterpreterConfiguration Python27 = GetCPythonVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Python35 = GetCPythonVersion(PythonLanguageVersion.V35, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Python36 = GetCPythonVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Python37 = GetCPythonVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Python38 = GetCPythonVersion(PythonLanguageVersion.V38, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Python27_x64 = GetCPythonVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x64);
        public static readonly InterpreterConfiguration Python35_x64 = GetCPythonVersion(PythonLanguageVersion.V35, InterpreterArchitecture.x64);
        public static readonly InterpreterConfiguration Python36_x64 = GetCPythonVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x64);
        public static readonly InterpreterConfiguration Python37_x64 = GetCPythonVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x64);
        public static readonly InterpreterConfiguration Python38_x64 = GetCPythonVersion(PythonLanguageVersion.V38, InterpreterArchitecture.x64);
        public static readonly InterpreterConfiguration Anaconda27 = GetAnacondaVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Anaconda27_x64 = GetAnacondaVersion(PythonLanguageVersion.V27, InterpreterArchitecture.x64);
        public static readonly InterpreterConfiguration Anaconda36 = GetAnacondaVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Anaconda36_x64 = GetAnacondaVersion(PythonLanguageVersion.V36, InterpreterArchitecture.x64);
        public static readonly InterpreterConfiguration Anaconda37 = GetAnacondaVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x86);
        public static readonly InterpreterConfiguration Anaconda37_x64 = GetAnacondaVersion(PythonLanguageVersion.V37, InterpreterArchitecture.x64);

        public static IEnumerable<InterpreterConfiguration> AnacondaVersions => GetVersions(
            Anaconda36,
            Anaconda36_x64,
            Anaconda37,
            Anaconda37_x64,
            Anaconda27,
            Anaconda27_x64);

        public static IEnumerable<InterpreterConfiguration> Versions => GetVersions(
            Python27,
            Python27_x64,
            Python35,
            Python35_x64,
            Python36,
            Python36_x64,
            Python37,
            Python37_x64,
            Python38,
            Python38_x64);

        public static InterpreterConfiguration Required_Python27X => Python27 ?? Python27_x64 ?? NotInstalled("v2.7");
        public static InterpreterConfiguration Required_Python35X => Python35 ?? Python35_x64 ?? NotInstalled("v3.5");
        public static InterpreterConfiguration Required_Python36X => Python36 ?? Python36_x64 ?? NotInstalled("v3.6");
        public static InterpreterConfiguration Required_Python37X => Python37 ?? Python37_x64 ?? NotInstalled("v3.7");
        public static InterpreterConfiguration Required_Python38X => Python38 ?? Python38_x64 ?? NotInstalled("v3.8");

        public static InterpreterConfiguration LatestAvailable => LatestAvailable3X ?? LatestAvailable2X;

        public static InterpreterConfiguration LatestAvailable3X => GetVersions(
            Python38,
            Python38_x64,
            Python37,
            Python37_x64,
            Python36,
            Python36_x64,
            Python35,
            Python35_x64).FirstOrDefault() ?? NotInstalled("v3");

        public static InterpreterConfiguration LatestAvailable2X => GetVersions(
            Python27,
            Python27_x64).FirstOrDefault() ?? NotInstalled("v2");

        public static InterpreterConfiguration LatestAnaconda3X => GetVersions(
            Anaconda37,
            Anaconda37_x64,
            Anaconda36,
            Anaconda36_x64).FirstOrDefault() ?? NotInstalled("Anaconda v3");

        public static InterpreterConfiguration LatestAnaconda2X => GetVersions(
            Anaconda27,
            Anaconda27_x64).FirstOrDefault() ?? NotInstalled("Anaconda v2");

        public static InterpreterConfiguration EarliestAvailable => EarliestAvailable2X ?? EarliestAvailable3X;

        public static InterpreterConfiguration EarliestAvailable3X => GetVersions(
            Python35,
            Python35_x64,
            Python36,
            Python36_x64,
            Python37,
            Python37_x64,
            Python38,
            Python38_x64).FirstOrDefault() ?? NotInstalled("v3");

        public static InterpreterConfiguration EarliestAvailable2X => GetVersions(
            Python27,
            Python27_x64).FirstOrDefault() ?? NotInstalled("v2");

        public static InterpreterConfiguration GetRequiredCPythonConfiguration(PythonLanguageVersion version)
            => GetCPythonVersion(version, InterpreterArchitecture.x86) ?? GetCPythonVersion(version, InterpreterArchitecture.x64) ?? NotInstalled(version.ToString());

        private static IEnumerable<InterpreterConfiguration> GetVersions(params InterpreterConfiguration[] configurations) => configurations.Where(v => v != null);

        private static InterpreterConfiguration GetCPythonVersion(PythonLanguageVersion version, InterpreterArchitecture arch)
            => PythonInstallPathResolver.Instance.GetCorePythonConfiguration(arch, version.ToVersion());

        private static InterpreterConfiguration GetAnacondaVersion(PythonLanguageVersion version, InterpreterArchitecture arch)
            => PythonInstallPathResolver.Instance.GetCondaPythonConfiguration(arch, version.ToVersion());

        private static InterpreterConfiguration NotInstalled(string version) {
            Assert.Inconclusive($"Python interpreter {version} is not installed");
            return null;
        }
    }
}
