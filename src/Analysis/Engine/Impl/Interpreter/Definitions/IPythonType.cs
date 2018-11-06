// Python Tools for Visual Studio
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

namespace Microsoft.PythonTools.Interpreter {
    public interface IPythonType : IMemberContainer, IMember {
        IPythonFunction GetConstructors();

        // PythonType.Get__name__(this);
        string Name { get; }

        /// <summary>
        /// Human-readable documentation that may be displayed in the editor hover tooltip.
        /// </summary>
        string Documentation { get; }

        BuiltinTypeId TypeId { get; }

        IPythonModule DeclaringModule { get; }

        IReadOnlyList<IPythonType> Mro { get; }

        bool IsBuiltin { get; }
    }

    public interface IPythonType2 : IPythonType {
        /// <summary>
        /// Indicates that type is a class. Used in cases when function has to return
        /// a class rather than the class instance. Example: function annotated as '-> Type[T]'
        /// can be called as a T constructor so func() constructs class instance rather than invoking
        /// // call on an existing instance. See also collections/namedtuple typing in the Typeshed.
        /// </summary>
        bool IsClass { get; }
    }
}
