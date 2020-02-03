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
using System.Threading.Tasks;
using Microsoft.Python.Parsing;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LanguageServerClient {
    internal class PythonLanguageServerNodejs : PythonLanguageServer {
        private readonly JoinableTaskContext _joinableTaskContext;
        private Process _process;

        public PythonLanguageServerNodejs(JoinableTaskContext joinableTaskContext) {
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
        }

        private static bool IsEnabled => IsEnvVarEnabled("PTVS_NODE_SERVER_ENABLED");

        private static bool IsForcedOnPython2 => IsEnvVarEnabled("PTVS_NODE_SERVER_FORCED_ON_PYTHON2");

        public static bool IsPreferred(PythonLanguageVersion version) {
            if (!IsEnabled) {
                return false;
            }

            if (IsForcedOnPython2) {
                return true;
            }

            return version >= PythonLanguageVersion.V30 || version == PythonLanguageVersion.None;
        }

        public async override Task<Connection> ActivateAsync() {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            var nodePath = GetNodeExecutablePath();
            if (!File.Exists(nodePath)) {
                throw new FileNotFoundException("Node.js was not found. It is required to run the language server.");
            }

            var serverFolderPath = GetServerLocation();
            if (!Directory.Exists(serverFolderPath)) {
                throw new FileNotFoundException("Node.js based language server was not found.");
            }

            var serverFilePath = Path.Combine(serverFolderPath, @"server\server.bundle.js");
            if (!File.Exists(serverFilePath)) {
                throw new FileNotFoundException("Node.js based language server server, server.bundle.js was not found.");
            }

            await Task.Yield();

            var info = new ProcessStartInfo {
                FileName = nodePath,
                WorkingDirectory = serverFolderPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = serverFilePath,
            };

            _process = new Process {
                StartInfo = info
            };

            if (_process.Start()) {
                return new Connection(_process.StandardOutput.BaseStream, _process.StandardInput.BaseStream);
            }

            return null;
        }

        public override object CreateInitializationOptions(string interpreterPath, string interpreterVersion, string rootPath, IEnumerable<string> searchPaths) {
            return new PythonInitializationOptions {
                analysis = null,
                pythonPath = interpreterPath,
                disableLanguageServices = false,
                openFilesOnly = false,
                venvPath = null,
                useLibraryCodeForTypes = true,
            };
        }

        private static string GetServerLocation() {
            // At some point, we'll bundle it and return the install location
            // but for now, to allow for testing, location is retrieved via
            // environment variable.
            var folderPath = Environment.GetEnvironmentVariable("PTVS_NODE_SERVER_LOCATION");
            if (Directory.Exists(folderPath)) {
                return folderPath;
            }

            return null;
        }

        private static bool IsEnvVarEnabled(string variable) {
            var val = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(val)) {
                return false;
            }

            return val != "0" && string.Compare(val, "false", StringComparison.OrdinalIgnoreCase) != 0;
        }

        private string GetNodeExecutablePath() {
            string filePath;

            // Prefer the node executable from the server folder, if there is one
            var serverFolderPath = GetServerLocation();
            if (Directory.Exists(serverFolderPath)) {
                filePath = Path.Combine(serverFolderPath, "node.exe");
                if (File.Exists(filePath)) {
                    return filePath;
                }
            }

            // For development convenience, allow use of global node.js install
            if (Environment.Is64BitOperatingSystem) {
                filePath = GetNodePathFromRegistry(RegistryView.Registry64);
                if (File.Exists(filePath)) {
                    return filePath;
                }
            }

            filePath = GetNodePathFromRegistry(RegistryView.Registry32);
            if (File.Exists(filePath)) {
                return filePath;
            }

            return null;
        }

        private static string GetNodePathFromRegistry(RegistryView view) {
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (var key = root.OpenSubKey("Software\\Node.js")) {
                var installPath = key.GetValue("InstallPath", null) as string;
                if (Directory.Exists(installPath)) {
                    var filePath = Path.Combine(installPath, "node.exe");
                    if (File.Exists(filePath)) {
                        return filePath;
                    }
                }
            }

            return null;
        }

        public override void Dispose() {
            if (_process != null) {
                try {
                    _process.Kill();
                } catch (InvalidOperationException) { } catch (NotSupportedException) { }

                _process.WaitForExit();
                _process.Dispose();
                _process = null;
            }
        }

        [Serializable]
        public sealed class PythonInitializationOptions {
            [Serializable]
            public class AnalysisOptions {
                /// <summary>
                /// Paths to look for typeshed modules.
                /// </summary>
                public string[] typeshedPaths = Array.Empty<string>();
            }
            public AnalysisOptions analysis;

            /// <summary>
            /// Path to Python, you can use a custom version of Python.
            /// </summary>
            public string pythonPath;

            /// <summary>
            /// Path to folder with a list of Virtual Environments.
            /// </summary>
            public string venvPath;

            /// <summary>
            /// Disables type completion, definitions, and references.
            /// </summary>
            public bool disableLanguageServices;

            /// <summary>
            /// Report errors only for currently-open files.
            /// </summary>
            public bool openFilesOnly;

            /// <summary>
            /// Use library implementations to extract type information when type stub is not present.
            /// </summary>
            public bool useLibraryCodeForTypes;
        }
    }
}
