﻿// Copyright(c) Microsoft Corporation
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
    /// Represents a variable.
    /// </summary>
    public interface IVariable: ILocatedMember {
        /// <summary>
        /// Variable name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Variable source.
        /// </summary>
        VariableSource Source { get; }

        /// <summary>
        /// Variable value.
        /// </summary>
        IMember Value { get; }

        /// <summary>
        /// Variable represents class member.
        /// </summary>
        bool IsClassMember { get; }

        /// <summary>
        /// Assigns value to the variable.
        /// </summary>
        void Assign(IMember value, Location location);
    }
}
