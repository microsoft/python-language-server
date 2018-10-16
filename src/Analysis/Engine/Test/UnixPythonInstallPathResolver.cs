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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    internal class UnixPythonInstallPathResolver : IPythonInstallPathResolver {
        private static readonly Regex _pythonNameRegex = new Regex(@"^python(\d+(.\d+)?)?$", RegexOptions.Compiled);
        private readonly Dictionary<Version, InterpreterConfiguration> _coreCache;
        private readonly Dictionary<Version, InterpreterConfiguration> _condaCache;

        public UnixPythonInstallPathResolver() {
            _coreCache = new Dictionary<Version, InterpreterConfiguration>();
            _condaCache = new Dictionary<Version, InterpreterConfiguration>();
            GetConfigurationsFromKnownPaths();
            GetConfigurationsFromConda();
        }

        public InterpreterConfiguration GetCorePythonConfiguration(InterpreterArchitecture architecture, Version version)
            => architecture == InterpreterArchitecture.x86 ? null : _coreCache.TryGetValue(version, out var interpreterConfiguration) ? interpreterConfiguration : null;

        public InterpreterConfiguration GetCondaPythonConfiguration(InterpreterArchitecture architecture, Version version)
            => architecture == InterpreterArchitecture.x86 ? null : _condaCache.TryGetValue(version, out var interpreterConfiguration) ? interpreterConfiguration : null;

        public InterpreterConfiguration GetIronPythonConfiguration(bool x64) => null;

        private void GetConfigurationsFromKnownPaths() {
            var homePath = Environment.GetEnvironmentVariable("HOME");
            var foldersFromPathVariable = Environment.GetEnvironmentVariable("PATH")?.Split(':') ?? Array.Empty<string>();
            var knownFolders = new[] { "/usr/local/bin", "/usr/bin", "/bin", "/usr/sbin", "/sbin", "/usr/local/sbin" };
            var folders = knownFolders.Concat(knownFolders.Select(p => Path.Combine(homePath, p))).Union(foldersFromPathVariable);
            foreach (var folder in folders) {
                try {
                    var filePaths = Directory.EnumerateFiles(folder)
                        .Where(p => _pythonNameRegex.IsMatch(Path.GetFileName(p)));
                    foreach (var filePath in filePaths) {
                        var configuration = GetConfiguration("Python Core", filePath);
                        _coreCache.TryAdd(configuration.Version, configuration);
                    }
                } catch (IOException) {
                }
            }
        }

        private void GetConfigurationsFromConda() {
            var homePath = Environment.GetEnvironmentVariable("HOME");
            var condaEnvironmentsPath = Path.Combine(homePath, ".conda", "environments.txt");
            IEnumerable<string> paths;
            try {
                paths = File.ReadAllLines(condaEnvironmentsPath)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => Path.Combine(p.Trim(), "bin", "python"));
            } catch (IOException) {
                return;
            }

            foreach (var path in paths) {
                var configuration = GetConfiguration("Conda", path);
                _coreCache.TryAdd(configuration.Version, configuration);
                _condaCache.TryAdd(configuration.Version, configuration);
            }
        }

        private InterpreterConfiguration GetConfiguration(string idPrefix, string pythonFilePath) {
            var configurationStrings = GetConfigurationString(pythonFilePath);
            var version = Version.Parse(configurationStrings[0]);
            var prefix = configurationStrings[1];
            var architecture = bool.Parse(configurationStrings[2])
                ? InterpreterArchitecture.x64
                : InterpreterArchitecture.x86;
            var libPath = GetLibraryLocation(pythonFilePath);
            var sitePackagesPath = GetSitePackagesLocation(pythonFilePath);

            return new InterpreterConfiguration(
                id: $"{idPrefix}|{version}",
                description: $"{idPrefix} {version} ({architecture})",
                pythonExePath: pythonFilePath,
                pathVar: pythonFilePath,
                libPath: libPath,
                sitePackagesPath: sitePackagesPath,
                architecture: architecture,
                version: version);
        }

        private static string[] GetConfigurationString(string pythonFilePath)
            => RunPythonAndGetOutput(pythonFilePath,
                    "-c \"import sys; print('.'.join(str(x) for x in sys.version_info[:2])); print(sys.prefix); print(sys.maxsize > 2**32)\"");

        private static string GetLibraryLocation(string pythonFilePath)
            => RunPythonAndGetOutput(pythonFilePath,
                    "-c \"import os, inspect; print(os.path.dirname(inspect.getfile(os)))\"").FirstOrDefault();

        private static string GetSitePackagesLocation(string pythonFilePath)
            => RunPythonAndGetOutput(pythonFilePath,
                    "-c \"import site; print(site.getsitepackages())\"").FirstOrDefault(s => s.Contains("site-packages"));

        private static string[] RunPythonAndGetOutput(string pythonFilePath, string arguments) {
            try {
                var processStartInfo = new ProcessStartInfo {
                    FileName = pythonFilePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(processStartInfo);
                var result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            } catch (Exception) {
                return Array.Empty<string>();
            }
        }
    }
}