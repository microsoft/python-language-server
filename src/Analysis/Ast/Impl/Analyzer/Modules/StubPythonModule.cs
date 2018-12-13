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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    /// <summary>
    /// Represents module that contains stub code such as from typeshed.
    /// </summary>
    internal class StubPythonModule : ScrapedPythonModule {
        private readonly string _cachePath;

        public static IPythonModule FromTypeStub(
            string moduleFullName,
            string stubFile,
            IServiceContainer services
        ) => new StubPythonModule(moduleFullName, stubFile, services);

        public StubPythonModule(string name, string cachePath, IServiceContainer services)
            : base(name, null, services) {
            _cachePath = cachePath;
        }

        protected override string LoadCachedCode() {
            var filePath = _cachePath;
            if(FileSystem.DirectoryExists(_cachePath)) {
                filePath = Path.Combine(_cachePath, Name);
                if(!FileSystem.FileExists(filePath)) {
                    return string.Empty;
                }
            }

            try {
                return FileSystem.ReadAllText(filePath);
            } catch (IOException) { } catch(UnauthorizedAccessException) { }

            return string.Empty;
        }

        protected override IEnumerable<string> GetScrapeArguments(IPythonInterpreter factory) => Enumerable.Empty<string>();
        protected override void SaveCachedCode(string code) { }
    }
}
