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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.Analysis.Analyzer {
    internal interface IAnalysisCache {
        /// <summary>
        /// Returns path to the stub file for the given module.
        /// Typically used to store stubs generated from compiled modules.
        /// </summary>
        string GetStubCacheFilePath(string moduleName, string content);

        /// <summary>
        /// Writes document analysis to a disk file.
        /// </summary>
        Task WriteAnalysisAsync(IDocument document, CancellationToken cancellationToken = default);

        /// <summary>
        /// Given fully qualified name of a module member such as function, class or a class member
        /// returns name of the type that member returns.
        /// </summary>
        string GetMemberValueTypeName(string fullyQualifiedMemberName);
    }
}
