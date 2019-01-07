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

using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Represents instance of a type.
    /// </summary>
    public interface IPythonInstance : ILocatedMember {
        /// <summary>
        /// Type of the object the instance represents.
        /// </summary>
        IPythonType Type { get; }

        /// <summary>
        /// Invokes method or property on the instance.
        /// </summary>
        /// <param name="memberName">Method name.</param>
        /// <param name="args">Call arguments.</param>
        IMember Call(string memberName, params object[] args);

        /// <summary>
        /// Invokes indexer the instance.
        /// </summary>
        IMember Index(object index);
    }
}
