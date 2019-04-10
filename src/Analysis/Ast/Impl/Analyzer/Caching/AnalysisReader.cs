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
using Microsoft.Python.Core.IO;
using Newtonsoft.Json;

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
                var text = _fs.ReadTextWithRetry(filePath);
                return JsonSerializer.Create().Deserialize(new StringReader(text), typeof(ModuleData)) as ModuleData;
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            } catch (JsonException) {
                // The file is malformed.
                _fs.DeleteFileWithRetries(filePath);
            }
            return null;
        }
    }
}
