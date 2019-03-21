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

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Represents an instance of type or the type information.
    /// Actual instance has <see cref="MemberType"/> set to <see cref="PythonMemberType.Instance"/>.
    /// Type information is marked as the type it describes, such as <see cref="PythonMemberType.Class"/>.
    /// </summary>
    [DebuggerDisplay("Instance of {Type.Name}")]
    internal class PythonInstance : LocatedMember, IPythonInstance, IEquatable<IPythonInstance> {
        public PythonInstance(IPythonType type, Node definition = null) : base(definition) {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public virtual IPythonType Type { get; }
        public virtual PythonMemberType MemberType => PythonMemberType.Instance;

        public virtual IMember Call(string memberName, IArgumentSet args) {
            var t = Type.GetMember(memberName).GetPythonType();
            switch (t) {
                case IPythonFunctionType fn:
                    return fn.Call(this, null, args);
                case IPythonPropertyType prop:
                    return prop.Call(this, null, args);
                case IPythonClassType cls:
                    return cls.Call(this, null, args);
            }
            // Do NOT call type unless it is specific (see above) since by default Python type
            // implementation delegates down to the instance and this will yield stack overflow.
            return this;
        }

        public virtual IMember Index(object index) => this; // Helps with str slicing

        protected IMember UnknownType => Type.DeclaringModule.Interpreter.UnknownType;

        public virtual IPythonIterator GetIterator() {
            var iteratorFunc = Type.GetMember(@"__iter__") as IPythonFunctionType;
            var o = iteratorFunc?.Overloads.FirstOrDefault();
            var instance = o?.Call(ArgumentSet.Empty, Type);
            if (instance != null) {
                return new PythonInstanceIterator(instance, Type.DeclaringModule.Interpreter);
            }

            return new EmptyIterator(Type.DeclaringModule.Interpreter.UnknownType);
        }

        public bool Equals(IPythonInstance other) => Type?.Equals(other?.Type) == true;
    }
}
