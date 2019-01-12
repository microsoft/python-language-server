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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
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

            // NewType
            fn = new PythonFunctionType("NewType", this, null, GetMemberDocumentation, GetMemberLocation);
            o = new PythonFunctionOverload(fn.Name, this, _ => fn.Location);
            // When called, create generic parameter type. For documentation
            // use original TypeVar declaration so it appear as a tooltip.
            o.SetReturnValueProvider((interpreter, overload, location, args) => CreateTypeAlias(args));
            fn.AddOverload(o);
            _members["NewType"] = fn;

            // NewType
            fn = new PythonFunctionType("Type", this, null, GetMemberDocumentation, GetMemberLocation);
            o = new PythonFunctionOverload(fn.Name, this, _ => fn.Location);
            // When called, create generic parameter type. For documentation
            // use original TypeVar declaration so it appear as a tooltip.
            o.SetReturnValueProvider((interpreter, overload, location, args) => args.Count == 1 ? args[0].GetPythonType() : Interpreter.UnknownType);
            fn.AddOverload(o);
            _members["Type"] = fn;

            _members["Iterator"] = new GenericType("Iterator", this,
                (typeArgs, module, location) => CreateIteratorType(typeArgs));

            _members["Iterable"] = new GenericType("Iterable", this,
                (typeArgs, module, location) => CreateListType("Iterable", BuiltinTypeId.List, typeArgs, false));
            _members["Sequence"] = new GenericType("Sequence", this,
                (typeArgs, module, location) => CreateListType("Sequence", BuiltinTypeId.List, typeArgs, false));
            _members["MutableSequence"] = new GenericType("MutableSequence", this,
                (typeArgs, module, location) => CreateListType("MutableSequence", BuiltinTypeId.List, typeArgs, true));
            _members["List"] = new GenericType("List", this,
                (typeArgs, module, location) => CreateListType("List", BuiltinTypeId.List, typeArgs, true));

            _members["MappingView"] = new GenericType("MappingView", this,
                (typeArgs, module, location) => CreateDictionary("MappingView", typeArgs, false));
            _members["KeysView"] = new GenericType("KeysView", this,
                (typeArgs, module, location) => CreateKeysViewType(typeArgs));
            _members["ValuesView"] = new GenericType("ValuesView", this,
                (typeArgs, module, location) => CreateValuesViewType(typeArgs));
            _members["ItemsView"] = new GenericType("ItemsView", this,
                (typeArgs, module, location) => CreateItemsViewType(typeArgs));

            _members["Set"] = new GenericType("Set", this,
                (typeArgs, module, location) => CreateListType("Set", BuiltinTypeId.Set, typeArgs, true));
            _members["MutableSet"] = new GenericType("MutableSet", this,
                (typeArgs, module, location) => CreateListType("MutableSet", BuiltinTypeId.Set, typeArgs, true));
            _members["FrozenSet"] = new GenericType("FrozenSet", this,
                (typeArgs, module, location) => CreateListType("FrozenSet", BuiltinTypeId.Set, typeArgs, false));

            _members["Tuple"] = new GenericType("Tuple", this,
                (typeArgs, module, location) => CreateTupleType(typeArgs));

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

            _members["Union"] = new GenericType("Union", this,
                (typeArgs, module, location) => CreateUnion(typeArgs));

            _members["Counter"] = Specialized.Function("Counter", this, null, "Counter", new PythonInstance(Interpreter.GetBuiltinType(BuiltinTypeId.Int)));

            _members["SupportsInt"] = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
            _members["SupportsFloat"] = Interpreter.GetBuiltinType(BuiltinTypeId.Float);
            _members["SupportsComplex"] = Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
            _members["SupportsBytes"] = Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
            _members["ByteString"] = Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);

            fn = new PythonFunctionType("NamedTuple", this, null, GetMemberDocumentation, GetMemberLocation);
            o = new PythonFunctionOverload(fn.Name, this, _ => fn.Location);
            o.SetReturnValueProvider((interpreter, overload, location, args) => CreateNamedTuple(args));
            fn.AddOverload(o);
            _members["NamedTuple"] = fn;
        }


        private string GetMemberDocumentation(string name)
            => base.GetMember(name)?.GetPythonType()?.Documentation;
        private LocationInfo GetMemberLocation(string name)
            => (base.GetMember(name)?.GetPythonType() as ILocatedMember)?.Location ?? LocationInfo.Empty;

        private IPythonType CreateListType(string typeName, BuiltinTypeId typeId, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 1) {
                return TypingTypeFactory.CreateListType(Interpreter, typeName, typeId, typeArgs[0], isMutable);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateTupleType(IReadOnlyList<IPythonType> typeArgs)
            => TypingTypeFactory.CreateTupleType(Interpreter, typeArgs);

        private IPythonType CreateIteratorType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                return TypingTypeFactory.CreateIteratorType(Interpreter, typeArgs[0]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateDictionary(string typeName, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 2) {
                return TypingTypeFactory.CreateDictionary(Interpreter, typeName, typeArgs[0], typeArgs[1], isMutable);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateKeysViewType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                return TypingTypeFactory.CreateKeysViewType(Interpreter, typeArgs[0]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateValuesViewType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                return TypingTypeFactory.CreateValuesViewType(Interpreter, typeArgs[0]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateItemsViewType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 2) {
                return TypingTypeFactory.CreateItemsViewType(Interpreter, typeArgs[0], typeArgs[1]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateTypeAlias(IReadOnlyList<IMember> typeArgs) {
            if (typeArgs.Count == 2) {
                var typeName = (typeArgs[0] as IPythonConstant)?.Value as string;
                if (!string.IsNullOrEmpty(typeName)) {
                    return new TypeAlias(typeName, typeArgs[1].GetPythonType() ?? Interpreter.UnknownType);
                }
                // TODO: report incorrect first argument to NewVar
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateUnion(IReadOnlyList<IMember> typeArgs) {
            if (typeArgs.Count > 0) {
                return TypingTypeFactory.CreateUnion(Interpreter, typeArgs.Select(a => a.GetPythonType()).ToArray());
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateNamedTuple(IReadOnlyList<IMember> typeArgs) {
            if (typeArgs.Count != 2) {
                // TODO: report wrong number of arguments
                return Interpreter.UnknownType;
            }

            ;
            if (!typeArgs[0].TryGetConstant<string>(out var tupleName) || string.IsNullOrEmpty(tupleName)) {
                // TODO: report name is incorrect.
                return Interpreter.UnknownType;
            }

            var argList = (typeArgs[1] as IPythonCollection)?.Contents;
            if (argList == null) {
                // TODO: report type spec is not a list.
                return Interpreter.UnknownType;
            }

            var itemNames = new List<string>();
            var itemTypes = new List<IPythonType>();
            foreach (var pair in argList) {
                if (!(pair is IPythonCollection c) || c.Type.TypeId != BuiltinTypeId.Tuple) {
                    // TODO: report that item is not a tuple.
                    continue;
                }
                if (c.Contents.Count != 2) {
                    // TODO: report extra items in the element spec.
                    continue;
                }
                if (!c.Contents[0].TryGetConstant<string>(out var itemName)) {
                    // TODO: report item name is not a string.
                    continue;
                }
                itemNames.Add(itemName);
                itemTypes.Add(c.Contents[1].GetPythonType());
            }
            return TypingTypeFactory.CreateNamedTuple(Interpreter, tupleName, itemNames, itemTypes);
        }
    }
}
