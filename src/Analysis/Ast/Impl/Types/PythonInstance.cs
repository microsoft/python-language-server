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

using System.Diagnostics;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents instance of the type, such as instance of a class,
    /// rather than the class type itself.
    /// </summary>
    [DebuggerDisplay("Instance of {Type.Name}")]
    internal class PythonInstance : PythonTypeWrapper, IPythonInstance {
        public PythonInstance(IPythonType type, LocationInfo location) : base(type) {
            Location = location ?? LocationInfo.Empty;
        }

        public override LocationInfo Location { get; }
        public IPythonType Type => InnerType;
    }
}
