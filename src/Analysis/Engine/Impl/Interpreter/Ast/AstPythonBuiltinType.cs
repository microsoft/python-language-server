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

using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonBuiltinType : AstPythonType {
        private BuiltinTypeId _typeId;

        public AstPythonBuiltinType(string name, BuiltinTypeId typeId)
            : base(name) {
            _typeId = typeId;
        }

        public AstPythonBuiltinType(
            string name,
            IPythonModule declaringModule,
            string doc,
            LocationInfo loc,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown,
            bool isClassFactory = false
        ) : base(name, declaringModule, doc, loc, isClassFactory) {
            _typeId = typeId;
        }

        public bool TrySetTypeId(BuiltinTypeId typeId) {
            if (_typeId != BuiltinTypeId.Unknown) {
                return false;
            }
            _typeId = typeId;
            return true;
        }

        #region IPythonType
        public override bool IsBuiltIn => true;
        public override BuiltinTypeId TypeId => _typeId;
        #endregion

        public bool IsHidden => ContainsMember("__hidden__");

        /// <summary>
        /// Provides class factory. Similar to __metaclass__ but does not expose full
        /// metaclass functionality. Used in cases when function has to return a class
        /// rather than the class instance. Example: function annotated as '-> Type[T]'
        /// can be called as a T constructor so func() constructs class instance rather than invoking
        /// call on an existing instance. See also collections/namedtuple typing in the Typeshed.
        /// </summary>
        internal AstPythonBuiltinType GetClassFactory() {
            var clone = new AstPythonBuiltinType(Name, DeclaringModule, Documentation,
               Locations.OfType<LocationInfo>().FirstOrDefault(),
               TypeId == BuiltinTypeId.Unknown ? BuiltinTypeId.Type : TypeId);
            clone.AddMembers(Members, true);
            return clone;
        }
    }
}
