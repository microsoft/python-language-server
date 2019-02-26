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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents module that contains stub code such as from typeshed.
    /// </summary>
    internal class StubPythonModule : CompiledPythonModule, IPythonStubModule {
        public bool IsTypeshed { get; }

        public StubPythonModule(string moduleName, string stubPath, bool isTypeshed, IServiceContainer services)
            : base(moduleName, ModuleType.Stub, stubPath, null, services) {
            IsTypeshed = isTypeshed;
        }

        public IPythonModule PrimaryModule { get; set; }

        protected override string LoadContent() {
            // Exceptions are handled in the base
            return FileSystem.FileExists(FilePath) ? FileSystem.ReadAllText(FilePath) : string.Empty;
        }

        protected override IEnumerable<string> GetScrapeArguments(IPythonInterpreter factory) => Enumerable.Empty<string>();
        protected override void SaveCachedCode(string code) { }
    }
}
