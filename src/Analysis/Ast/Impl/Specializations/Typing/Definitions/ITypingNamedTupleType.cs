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

namespace Microsoft.Python.Analysis.Specializations.Typing {
    /// <summary>
    /// Represents typing.NamedTuple.
    /// </summary>
    public interface ITypingNamedTupleType : ITypingTupleType {
        IReadOnlyList<string> ItemNames { get; }
        /// <summary>
        /// Allows setting alternative name to the tuple at the variable assignment time.
        /// </summary>
        /// <remarks>
        /// Named tuple may get assigned to variables that have name different from the tuple itself.
        /// Then the name may conflict with other types in module or its persistent model. For example,
        /// 'tokenize' stub declares _TokenInfo = NamedTuple('TokenInfo', ...) but there is also
        /// 'class TokenInfo(_TokenInfo)'' so we have to use the variable name in order to avoid type conflicts.
        /// </remarks>
        /// <param name="name"></param>
        void SetName(string name);
    }
}
