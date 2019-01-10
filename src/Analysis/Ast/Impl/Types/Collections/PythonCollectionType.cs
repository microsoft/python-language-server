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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Types.Collections {
    /// <summary>
    /// Type info for an iterable entity. Most base collection class.
    /// </summary>
    internal class PythonCollectionType : PythonTypeWrapper, IPythonCollectionType {
        private string _typeName;

        /// <summary>
        /// Creates type info for an collection.
        /// </summary>
        /// <param name="typeName">Iterable type name. If null, name of the type id will be used.</param>
        /// <param name="collectionTypeId">Collection type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="interpreter">Python interpreter.</param>
        /// <param name="isMutable">Indicates if collection is mutable (like list) or immutable (like tuple).</param>
        public PythonCollectionType(
            string typeName,
            BuiltinTypeId collectionTypeId,
            IPythonInterpreter interpreter,
            bool isMutable
        ) : base(collectionTypeId, interpreter.ModuleResolution.BuiltinsModule) {
            _typeName = typeName;
            TypeId = collectionTypeId;
            IteratorType = new PythonIteratorType(collectionTypeId.GetIteratorTypeId(), interpreter);
            IsMutable = isMutable;
        }

        #region IPythonCollectionType
        /// <summary>
        /// Indicates if collection is mutable (such as list) or not (such as tuple).
        /// </summary>
        public bool IsMutable { get; }
        public virtual IPythonIterator GetIterator(IPythonInstance instance) => (instance as IPythonCollection)?.GetIterator();
        public IPythonIteratorType IteratorType { get; }
        #endregion

        #region IPythonType
        public override string Name {
            get {
                if (_typeName == null) {
                    var type = DeclaringModule.Interpreter.GetBuiltinType(TypeId);
                    if (!type.IsUnknown()) {
                        _typeName = type.Name;
                    }
                }
                return _typeName ?? "<not set>"; ;
            }
        }

        public override BuiltinTypeId TypeId { get; }
        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IMember GetMember(string name) => name == @"__iter__" ? IteratorType : base.GetMember(name);

        public override IMember CreateInstance(string typeName, LocationInfo location, IReadOnlyList<object> args)
            => new PythonCollection(this, location, args.OfType<IMember>().ToArray());

        // Constructor call
        public override IMember Call(IPythonInstance instance, string memberName, IReadOnlyList<object> args)
            => CreateInstance(Name, instance?.Location ?? LocationInfo.Empty, args);
        #endregion

        public static IPythonCollection CreateList(IPythonInterpreter interpreter, LocationInfo location, IReadOnlyList<IMember> contents, bool flatten = true) {
            var collectionType = new PythonCollectionType(null, BuiltinTypeId.List, interpreter, true);
            return new PythonCollection(collectionType, location, contents, flatten);
        }

        public static IPythonCollection CreateTuple(IPythonInterpreter interpreter, LocationInfo location, IReadOnlyList<IMember> contents) {
            var collectionType = new PythonCollectionType(null, BuiltinTypeId.Tuple, interpreter, false);
            return new PythonCollection(collectionType, location, contents);
        }
    }
}
