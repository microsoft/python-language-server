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
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents module that contains stub code such as from typeshed.
    /// </summary>
    internal class StubPythonModule : CompiledPythonModule {
        public StubPythonModule(string moduleName, string stubPath, IServiceContainer services)
            : base(moduleName, ModuleType.Stub, stubPath, null, services) {
        }

        protected override string LoadContent(ModuleLoadOptions options) {
            try {
                if (FileSystem.FileExists(FilePath) && (options & ModuleLoadOptions.Load) == ModuleLoadOptions.Load) {
                    return FileSystem.ReadAllText(FilePath);
                }
            } catch (IOException) { } catch(UnauthorizedAccessException) { }
            return string.Empty;
        }

        protected override IEnumerable<string> GetScrapeArguments(IPythonInterpreter factory) => Enumerable.Empty<string>();
        protected override void SaveCachedCode(string code) { }
    }
}
