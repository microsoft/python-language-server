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

namespace Microsoft.Python.Analysis.Caching {
    /// <summary>
    /// Describes module data stored in a database.
    /// </summary>
    public enum ModuleStorageState {
        /// <summary>
        /// Module does not exist in the database.
        /// </summary>
        DoesNotExist,

        /// <summary>
        /// Partial data. This means module is still being analyzed
        /// and the data on the module members is incomplete.
        /// </summary>
        Partial,

        /// <summary>
        /// Modules exist and the analysis is complete.
        /// </summary>
        Complete,

        /// <summary>
        /// Storage is corrupted or incompatible.
        /// </summary>
        Corrupted
    }
}
