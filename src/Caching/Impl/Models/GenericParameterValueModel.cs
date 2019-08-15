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

namespace Microsoft.Python.Analysis.Caching.Models {
    /// <summary>
    /// Model for actual values assigned to generic parameters.
    /// I.e. if class is based on Generic[T], what is assigned to T.
    /// </summary>
    internal sealed class GenericParameterValueModel {
        /// <summary>
        /// Generic parameter name as defined by TypeVar, such as T.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Qualified name of the type assigned to T.
        /// </summary>
        public string Type { get; set; }
    }
}
