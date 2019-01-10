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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Specializations.Typing {
    internal sealed class TypingModule : SpecializedModule {
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();

        private TypingModule(string modulePath, IServiceContainer services) : base("typing", modulePath, services) { }

        public static async Task<IPythonModule> CreateAsync(IServiceContainer services, CancellationToken cancellationToken = default) {
            var interpreter = services.GetService<IPythonInterpreter>();
            var module = await interpreter.ModuleResolution
                .SpecializeModuleAsync("typing", modulePath => new TypingModule(modulePath, services), cancellationToken) as TypingModule;
            module?.SpecializeMembers();
            return module;
        }

        #region IMemberContainer
        public override IMember GetMember(string name) => _members.TryGetValue(name, out var m) ? m : null;
        public override IEnumerable<string> GetMemberNames() => _members.Keys;
        #endregion

        private void SpecializeMembers() {
            // TypeVar
            var fn = new PythonFunctionType("TypeVar", this, null, GetMemberDocumentation, GetMemberLocation);
            var o = new PythonFunctionOverload(fn.Name, this, _ => fn.Location);
            // When called, create generic parameter type. For documentation
            // use original TypeVar declaration so it appear as a tooltip.
            o.SetReturnValueProvider((interpreter, overload, location, args) => GenericTypeParameter.FromTypeVar(args, interpreter, location));

            fn.AddOverload(o);
            _members["TypeVar"] = fn;

            _members["Iterator"] = new GenericType("Iterator", this,
                (typeArgs, module, location) => CreateIterator(typeArgs));

            _members["Iterable"] = new GenericType("Iterable", this,
                (typeArgs, module, location) => CreateList("Iterable", BuiltinTypeId.List, typeArgs, false));
            _members["Sequence"] = new GenericType("Sequence", this,
                (typeArgs, module, location) => CreateList("Sequence", BuiltinTypeId.List, typeArgs, false));
            _members["MutableSequence"] = new GenericType("MutableSequence", this,
                (typeArgs, module, location) => CreateList("MutableSequence", BuiltinTypeId.List, typeArgs, true));
            _members["List"] = new GenericType("List", this,
                (typeArgs, module, location) => CreateList("List", BuiltinTypeId.List, typeArgs, true));

            _members["MappingView"] = new GenericType("MappingView", this,
                (typeArgs, module, location) => CreateList("MappingView", BuiltinTypeId.List, typeArgs, false));
            _members["KeysView"] = new GenericType("KeysView", this,
                (typeArgs, module, location) => CreateList("KeysView", BuiltinTypeId.List, typeArgs, false));
            _members["ValuesView"] = new GenericType("ValuesView", this,
                (typeArgs, module, location) => CreateList("ValuesView", BuiltinTypeId.List, typeArgs, false));
            _members["ItemsView"] = new GenericType("ItemsView", this,
                (typeArgs, module, location) => CreateList("ItemsView", BuiltinTypeId.List, typeArgs, false));

            _members["Set"] = new GenericType("Set", this,
                (typeArgs, module, location) => CreateList("Set", BuiltinTypeId.Set, typeArgs, true));
            _members["MutableSet"] = new GenericType("MutableSet", this,
                (typeArgs, module, location) => CreateList("MutableSet", BuiltinTypeId.Set, typeArgs, true));
            _members["FrozenSet"] = new GenericType("FrozenSet", this,
                (typeArgs, module, location) => CreateList("FrozenSet", BuiltinTypeId.Set, typeArgs, false));

            _members["Tuple"] = new GenericType("Tuple", this,
                (typeArgs, module, location) => CreateTuple(typeArgs));

            _members["Mapping"] = new GenericType("Mapping", this,
                (typeArgs, module, location) => CreateDictionary("Mapping", typeArgs, false));
            _members["MutableMapping"] = new GenericType("MutableMapping", this,
                (typeArgs, module, location) => CreateDictionary("MutableMapping", typeArgs, true));
            _members["Dict"] = new GenericType("Dict", this,
                (typeArgs, module, location) => CreateDictionary("Dict", typeArgs, true));
            _members["OrderedDict"] = new GenericType("OrderedDict", this,
                (typeArgs, module, location) => CreateDictionary("OrderedDict", typeArgs, true));
            _members["DefaultDict"] = new GenericType("DefaultDict", this,
                (typeArgs, module, location) => CreateDictionary("DefaultDict", typeArgs, true));

            _members["Counter"] = Specialized.Function("Counter", this, null, "Counter", new PythonInstance(Interpreter.GetBuiltinType(BuiltinTypeId.Int)));
        }


        private string GetMemberDocumentation(string name)
            => base.GetMember(name)?.GetPythonType()?.Documentation;
        private LocationInfo GetMemberLocation(string name)
            => (base.GetMember(name)?.GetPythonType() as ILocatedMember)?.Location ?? LocationInfo.Empty;

        private IPythonType CreateList(string typeName, BuiltinTypeId typeId, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 1) {
                return new TypingListType(typeName, typeId, typeArgs[0], Interpreter, isMutable);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateTuple(IReadOnlyList<IPythonType> typeArgs) => new TypingTupleType(typeArgs, Interpreter);

        private IPythonType CreateIterator(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                return new TypingIteratorType(typeArgs[0], BuiltinTypeId.ListIterator, Interpreter);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateDictionary(string typeName, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 2) {
                return new TypingDictionaryType(typeName, typeArgs[0], typeArgs[1], Interpreter, isMutable);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }
    }
}
