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

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Describes variable source. Used in filtering variables during completion
    /// so the list does not show what the imported module has imported itself
    /// or what generic variables it has declared for internal purposes.
    /// </summary>
    public enum VariableSource {
        /// <summary>
        /// Variable is user declaration.
        /// </summary>
        Declaration,
        /// <summary>
        /// Variable is import from another module.
        /// </summary>
        Import,
        /// <summary>
        /// Variable is a generic type definition.
        /// </summary>
        Generic,

        /// <summary>
        /// Variable is as reference to existing variable
        /// declared as nonlocal or global.
        /// </summary>
        Locality
    }
}
