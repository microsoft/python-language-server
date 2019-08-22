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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Base class for specialized modules. Specialized modules are implementations
    /// that replace real Python module in imports. Content is loaded from the
    /// original module and analyzed only for the class/functions documentation.
    /// </summary>
    /// <remarks>
    /// Specialization is helpful when it is easier to express module members
    /// behavior to the analyzer in code. Example of specialization is 'typing'
    /// module. Specialized module can use actual library module as a source
    /// of documentation for its members. See <see cref="TypingModule"/>.
    /// </remarks>
    internal abstract class SpecializedModule : PythonModule {
        protected SpecializedModule(string name, string modulePath, IServiceContainer services)
            : base(name, modulePath, ModuleType.Specialized, null, services) { }

        protected override string LoadContent() {
            // Exceptions are handled in the base
            return FileSystem.FileExists(FilePath) ? FileSystem.ReadTextWithRetry(FilePath) : string.Empty;
        }

        #region IDependencyProvider
        public override HashSet<AnalysisModuleKey> GetDependencies() => new HashSet<AnalysisModuleKey>();
        #endregion
    }
}
