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

namespace Microsoft.Python.Analysis.Documents {
    [Flags]
    public enum DocumentCreationOptions {
        /// <summary>
        /// Do nothing. Typically this is a placeholder or empty module.
        /// </summary>
        None,

        /// <summary>
        /// Just load the document, do not parse or analyze.
        /// </summary>
        Load = 1,

        /// <summary>
        /// Load and parse. Do not analyze.
        /// </summary>
        Ast = Load | 2,

        /// <summary>
        /// Load, parse and analyze.
        /// </summary>
        Analyze = Ast | 4,

        /// <summary>
        /// The document is opened in the editor.
        /// This implies Ast and Analysis.
        /// </summary>
        Open = Analyze | 8
    }
}
