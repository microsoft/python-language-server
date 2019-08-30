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

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Type information of an instance.
    /// </summary>
    public interface IPythonType : ILocatedMember, IMemberContainer {
        /// <summary>
        /// Type name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Indicates built-in type id such as 'int' or 'str'
        /// or 'type' for user-defined entities.
        /// </summary>
        BuiltinTypeId TypeId { get; }

        /// <summary>
        /// Human-readable documentation that may be displayed in the editor hover tooltip.
        /// </summary>
        string Documentation { get; }

        /// <summary>
        /// Indicates if type is a built-in type.
        /// </summary>
        bool IsBuiltin { get; }

        /// <summary>
        /// Indicates if type is an abstract type.
        /// </summary>
        bool IsAbstract { get; }

        /// <summary>
        /// Indicates if type is specialized in code.
        /// </summary>
        bool IsSpecialized { get; }

        /// <summary>
        /// Create instance of the type, if any.
        /// </summary>
        /// <param name="typeName">Name of the type. Used in specialization scenarios
        /// where constructor may want to create specialized type.</param>
        /// <param name="args">Any custom arguments required to create the instance.</param>
        IPythonInstance CreateInstance(IArgumentSet args = null);

        /// <summary>
        /// Invokes method or property on the specified instance.
        /// </summary>
        /// <param name="instance">Instance of the type.</param>
        /// <param name="memberName">Member name to call, if applicable.</param>
        /// <param name="argSet">Call arguments.</param>
        IMember Call(IPythonInstance instance, string memberName, IArgumentSet argSet);

        /// <summary>
        /// Invokes indexer on the specified instance.
        /// </summary>
        /// <param name="instance">Instance of the type.</param>
        /// <param name="index">Index arguments.</param>
        IMember Index(IPythonInstance instance, IArgumentSet args);
    }
}
