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
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class AnalysisReader {
        private readonly IFileSystem _fs;
        private readonly Dictionary<string, ModuleData> _modules = new Dictionary<string, ModuleData>();
        private readonly string _cacheRootFolder;

        public AnalysisReader(string cacheRootFolder, IFileSystem fs) {
            _cacheRootFolder = cacheRootFolder;
            _fs = fs;
        }

        public ModuleData GetModuleData(string moduleName, string content) {
            if (!_modules.TryGetValue(moduleName, out var data)) {
                _modules[moduleName] = data = LoadModuleData(moduleName, content);
            }
            return data;
        }

        private ModuleData LoadModuleData(string moduleName, string content) {
            var filePath = CacheFolders.GetAnalysisCacheFilePath(_cacheRootFolder, moduleName, content, _fs);
            if (!_fs.FileExists(filePath)) {
                return null;
            }

            try {
                var lines = _fs.FileReadAllLines(filePath).ToArray();
                var md = new ModuleData();

                for (var i = 0; i < lines.Length; i++) {
                    var line = lines[i];

                    if (line.Length > 0 && line[i] == '#') {
                        continue;
                    }

                    if (line.StartsWith("c:")) {
                        var classData = ReadClass(lines, ref i, out var className);
                        md.Classes[className] = classData;
                    } else if (line.StartsWith("f:")) {
                        ReadItem(line, out var functionName, out var returnValue);
                        md.Functions[functionName] = returnValue;
                    } else {
                        Check.InvalidOperation(false);
                    }
                }
                return md;
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            } catch (InvalidOperationException) {
                // The file is malformed.
                _fs.DeleteFileWithRetries(filePath);
            }
            return null;
        }

        private ClassData ReadClass(string[] lines, ref int i, out string className) {
            var cd = new ClassData();
            className = null;

            for (; i < lines.Length; i++) {
                var line = lines[i];

                if (line.StartsWith("e:")) {
                    break;
                }

                if (line.StartsWith("c:")) {
                    var classData = ReadClass(lines, ref i, out var cn);
                    cd.Classes[cn] = classData;
                }

                if (line.StartsWith("f:")) {
                    ReadItem(line, out var itemName, out var returnValue);
                    cd.Methods[itemName] = returnValue;
                }

                if (line.StartsWith("p:")) {
                    ReadItem(line, out var itemName, out var returnValue);
                    cd.Properties[itemName] = returnValue;
                }

                if (line.StartsWith("m:")) {
                    ReadItem(line, out var itemName, out var returnValue);
                    cd.Fields[itemName] = returnValue;
                }
            }

            return cd;
        }

        /// <summary>
        /// Reads function name and return type from the cache file line
        /// such as 'f: name returnType.
        /// </summary>
        private void ReadItem(string line, out string itemName, out string returnValue) {
            var chunks = line.Split(new[] { ' ' });
            Check.InvalidOperation(chunks.Length == 3);
            itemName = chunks[1];
            returnValue = chunks[2];
        }
    }
}
