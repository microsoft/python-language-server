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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Analysis {
    internal sealed class WindowsPythonInstallPathResolver : IPythonInstallPathResolver {
        private readonly List<InterpreterConfiguration> _registryCache;

        public WindowsPythonInstallPathResolver() {
            _registryCache = FindPythonConfigurationsInRegistry();
        }

        public InterpreterConfiguration GetCorePythonConfiguration(InterpreterArchitecture architecture, Version version)
            => GetPythonConfiguration("Global|PythonCore|", architecture, version);

        public InterpreterConfiguration GetCondaPythonConfiguration(InterpreterArchitecture architecture, Version version)
            => GetPythonConfiguration("Global|ContinuumAnalytics|", architecture, version);

        private InterpreterConfiguration GetPythonConfiguration(string prefix, InterpreterArchitecture architecture, Version version)
            => _registryCache.FirstOrDefault(configuration =>
            configuration.Id.StartsWith(prefix) &&
            configuration.Architecture == architecture &&
            configuration.Version == version);

        public InterpreterConfiguration GetIronPythonConfiguration(bool x64) {
            var installPath = GetIronPythonInstallDir();
            if (!Directory.Exists(installPath)) {
                return null;
            }

            var exeName = x64 ? "ipy64.exe" : "ipy.exe";
            // IronPython changed to Any CPU for ipy.exe and ipy32.exe for 32-bit in 2.7.8
            if (File.Exists(Path.Combine(installPath, "ipy32.exe"))) {
                exeName = x64 ? "ipy.exe" : "ipy32.exe";
            }

            return new InterpreterConfiguration(
                id: x64 ? "IronPython|2.7-64" : "IronPython|2.7-32",
                description: string.Format("IronPython {0} 2.7", x64 ? "64-bit" : "32-bit"),
                pythonExePath: Path.Combine(installPath, exeName),
                libPath: Path.Combine(installPath, "Lib"),
                sitePackagesPath: Path.Combine(installPath, "Lib", "site-packages"),
                architecture: x64 ? InterpreterArchitecture.x64 : InterpreterArchitecture.x86,
                version: new Version(2, 7),
                pathVar: "IRONPYTHONPATH"
            );
        }

        private List<InterpreterConfiguration> FindPythonConfigurationsInRegistry() {
            var configurations = new List<InterpreterConfiguration>();

            using (var key = Registry.CurrentUser.OpenSubKey("Software\\Python")) {
                AddPythonConfigurationsFromRegistry(configurations, key, Environment.Is64BitOperatingSystem ? InterpreterArchitecture.Unknown : InterpreterArchitecture.x86);
            }

            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var key = root.OpenSubKey("Software\\Python")) {
                AddPythonConfigurationsFromRegistry(configurations, key, InterpreterArchitecture.x86);
            }

            if (Environment.Is64BitOperatingSystem) {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = root.OpenSubKey("Software\\Python")) {
                    AddPythonConfigurationsFromRegistry(configurations, key, InterpreterArchitecture.x64);
                }
            }

            InterpreterConfiguration.DisambiguateDescriptions(configurations);
            return configurations;
        }

        private void AddPythonConfigurationsFromRegistry(List<InterpreterConfiguration> configurations, RegistryKey key, InterpreterArchitecture assumedArchitecture) {
            if (key == null) {
                return;
            }

            var companies = key.GetSubKeyNames();
            foreach (var company in companies) {
                if ("PyLauncher".Equals(company, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                using (var companyKey = key.OpenSubKey(company)) {
                    if (companyKey == null) {
                        continue;
                    }

                    var tags = companyKey.GetSubKeyNames();
                    foreach (var tag in tags) {
                        using (var tagKey = companyKey.OpenSubKey(tag))
                        using (var installKey = tagKey?.OpenSubKey("InstallPath")) {
                            var config = TryReadConfiguration(company, tag, tagKey, installKey, assumedArchitecture);
                            if (config != null) {
                                configurations.Add(config);
                            }
                        }
                    }
                }
            }
        }

        private InterpreterConfiguration TryReadConfiguration(
            string company,
            string tag,
            RegistryKey tagKey,
            RegistryKey installKey,
            InterpreterArchitecture assumedArchitecture
        ) {
            if (tagKey == null || installKey == null) {
                return null;
            }

            var pythonCoreCompatibility = "PythonCore".Equals(company, StringComparison.OrdinalIgnoreCase);
            var prefixPath = PathUtils.NormalizePath(installKey.GetValue(null) as string);
            var exePath = PathUtils.NormalizePath(installKey.GetValue("ExecutablePath") as string);


            if (pythonCoreCompatibility && !string.IsNullOrEmpty(prefixPath)) {
                if (string.IsNullOrEmpty(exePath)) {
                    try {
                        exePath = Python.Tests.Utilities.PathUtils.GetAbsoluteFilePath(prefixPath, "python.exe");
                    } catch (ArgumentException) {
                    }
                }
            }

            var version = tagKey.GetValue("Version") as string;
            if (pythonCoreCompatibility && string.IsNullOrEmpty(version) && tag.Length >= 3) {
                version = tag.Substring(0, 3);
            }

            var sysVersionString = tagKey.GetValue("SysVersion") as string;
            if (pythonCoreCompatibility && string.IsNullOrEmpty(sysVersionString) && tag.Length >= 3) {
                sysVersionString = tag.Substring(0, 3);
            }

            if (string.IsNullOrEmpty(sysVersionString) || !Version.TryParse(sysVersionString, out var sysVersion)) {
                sysVersion = new Version(0, 0);
            }

            if (!InterpreterArchitecture.TryParse(tagKey.GetValue("SysArchitecture", null) as string, out var architecture)) {
                architecture = assumedArchitecture;
            }

            if (architecture == InterpreterArchitecture.Unknown && File.Exists(exePath)) {
                switch (GetBinaryType(exePath)) {
                    case ProcessorArchitecture.X86:
                        architecture = InterpreterArchitecture.x86;
                        break;
                    case ProcessorArchitecture.Amd64:
                        architecture = InterpreterArchitecture.x64;
                        break;
                }
            }

            if (pythonCoreCompatibility && sysVersion != null && sysVersion < new Version(3, 5) && architecture == InterpreterArchitecture.x86) {
                // Older versions of CPython did not include
                // "-32" in their Tag, so we will add it here
                // for uniqueness.
                tag += "-32";
            }

            var pathVar = tagKey.GetValue("PathEnvironmentVariable") as string ?? "PYTHONPATH";
            var id = $"Global|{company}|{tag}";

            var description = tagKey.GetValue("DisplayName") as string;
            if (string.IsNullOrEmpty(description)) {
                description = pythonCoreCompatibility ? "Python {0}{1: ()}".FormatUI(version, architecture) : "{0} {1}".FormatUI(company, tag);
            }

            return new InterpreterConfiguration(
                id: id,
                description: description,
                pythonExePath: exePath,
                pathVar: pathVar,
                libPath: Path.Combine(prefixPath, "Lib"),
                sitePackagesPath: Path.Combine(prefixPath, "Lib", "site-packages"),
                architecture: architecture,
                version: sysVersion
            );
        }

        private string GetIronPythonInstallDir() {
            // IronPython 2.7.7 and earlier use 32-bit registry
            var installPath = ReadIronPythonInstallPathFromRegistry(RegistryView.Registry32);
            if (!string.IsNullOrEmpty(installPath)) {
                return installPath;
            }

            // IronPython 2.7.8 and later use 64-bit registry
            installPath = ReadIronPythonInstallPathFromRegistry(RegistryView.Registry64);
            if (!string.IsNullOrEmpty(installPath)) {
                return installPath;
            }

            var paths = Environment.GetEnvironmentVariable("PATH");
            if (paths != null) {
                foreach (var dir in paths.Split(Path.PathSeparator)) {
                    try {
                        if (IronPythonExistsIn(dir)) {
                            return dir;
                        }
                    } catch {
                        // ignore
                    }
                }
            }

            return null;
        }

        private static string ReadIronPythonInstallPathFromRegistry(RegistryView view) {
            try {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var pathKey = baseKey.OpenSubKey("SOFTWARE\\IronPython\\2.7\\InstallPath")) {
                    return pathKey?.GetValue("") as string;
                }
            } catch (ArgumentException) {
            } catch (UnauthorizedAccessException) {
            }

            return null;
        }

        private static bool IronPythonExistsIn(string/*!*/ dir) => File.Exists(Path.Combine(dir, "ipy.exe"));

        [DllImport("kernel32", EntryPoint = "GetBinaryTypeW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
        private static extern bool _GetBinaryType(string lpApplicationName, out GetBinaryTypeResult lpBinaryType);

        private enum GetBinaryTypeResult : uint {
            SCS_32BIT_BINARY = 0,
            SCS_DOS_BINARY = 1,
            SCS_WOW_BINARY = 2,
            SCS_PIF_BINARY = 3,
            SCS_POSIX_BINARY = 4,
            SCS_OS216_BINARY = 5,
            SCS_64BIT_BINARY = 6
        }

        public static ProcessorArchitecture GetBinaryType(string path) {
            if (_GetBinaryType(path, out var result)) {
                switch (result) {
                    case GetBinaryTypeResult.SCS_32BIT_BINARY:
                        return ProcessorArchitecture.X86;
                    case GetBinaryTypeResult.SCS_64BIT_BINARY:
                        return ProcessorArchitecture.Amd64;
                    default:
                        return ProcessorArchitecture.None;
                }
            }

            return ProcessorArchitecture.None;
        }
    }
}
