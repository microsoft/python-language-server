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

using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching {
    /// <summary>
    /// Represents global scope that has been restored from
    /// the database but has not been fully populated yet.
    /// Used to attach to analysis so variables can be
    /// accessed during classes and methods restoration.
    /// </summary>
    internal interface IRestoredGlobalScope : IGlobalScope {
        void ReconstructVariables();
    }
}
