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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstTypeAnnotationConverter : TypeAnnotationConverter<IPythonType> {
        private readonly NameLookupContext _scope;

        public AstTypeAnnotationConverter(NameLookupContext scope) {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        /// <summary>
        /// Soft-casts a member to a type, extracting the type from
        /// a multi-member object if possible.
        /// </summary>
        private static IPythonType AsIPythonType(IMember m) {
            if (m is IPythonType t) {
                return t;
            }

            if (m is IPythonMultipleMembers mm) {
                return AstPythonMultipleMembers.CreateAs<IPythonType>(mm.GetMembers());
            }

            return null;
        }

        public override IPythonType Finalize(IPythonType type) {
            if (type == null || type is ModuleType) {
                return null;
            }

            if (type == _scope.UnknownType) {
                return null;
            }

            var n = GetName(type);
            if (!string.IsNullOrEmpty(n)) {
                return AsIPythonType(_scope.LookupNameInScopes(n));
            }

            return type;
        }

        private IEnumerable<IPythonType> FinalizeList(IPythonType type) {
            if (type is UnionType ut) {
                foreach (var t in ut.Types.MaybeEnumerate()) {
                    yield return Finalize(t);
                }
                yield break;
            }

            yield return Finalize(type);
        }

        public override IPythonType LookupName(string name) {
            var m = _scope.LookupNameInScopes(name, NameLookupContext.LookupOptions.Global | NameLookupContext.LookupOptions.Builtins);
            if (m is IPythonMultipleMembers mm) {
                m = (IMember)AstPythonMultipleMembers.CreateAs<IPythonType>(mm.GetMembers()) ??
                    AstPythonMultipleMembers.CreateAs<IPythonModule>(mm.GetMembers());
            }
            if (m is IPythonModule mod) {
                // Wrap the module in an IPythonType interface
                return new ModuleType(mod);
            }
            return m as IPythonType;
        }

        public override IPythonType GetTypeMember(IPythonType baseType, string member)
            => AsIPythonType(baseType.GetMember(_scope.Context, member));

        public override IPythonType MakeNameType(string name) => new NameType(name);
        public override string GetName(IPythonType type) => (type as NameType)?.Name;

        public override IPythonType MakeUnion(IReadOnlyList<IPythonType> types) => new UnionType(types);

        public override IReadOnlyList<IPythonType> GetUnionTypes(IPythonType type) =>
            type is UnionType unionType
                ? unionType.Types
                : type is IPythonMultipleMembers multipleMembers
                    ? multipleMembers.GetMembers().OfType<IPythonType>().ToArray()
                    : null;

        public override IPythonType MakeGeneric(IPythonType baseType, IReadOnlyList<IPythonType> args) {
            if (args == null || args.Count == 0 || baseType == null) {
                return baseType;
            }

            if (!AstTypingModule.IsTypingType(baseType) && !(baseType is NameType)) {
                return baseType;
            }

            switch (baseType.Name) {
                case "Tuple":
                case "Sequence":
                    return MakeSequenceType(BuiltinTypeId.Tuple, BuiltinTypeId.TupleIterator, args);
                case "List":
                    return MakeSequenceType(BuiltinTypeId.List, BuiltinTypeId.ListIterator, args);
                case "Set":
                    return MakeSequenceType(BuiltinTypeId.Set, BuiltinTypeId.SetIterator, args);
                case "Iterable":
                    return MakeIterableType(args);
                case "Iterator":
                    return MakeIteratorType(args);
                case "Dict":
                case "Mapping":
                    return MakeLookupType(BuiltinTypeId.Dict, args);
                case "Optional":
                    return Finalize(args.FirstOrDefault()) ?? _scope.UnknownType;
                case "Union":
                    return MakeUnion(args);
                case "ByteString":
                    return _scope.Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
                case "Type":
                    return args.Count > 0 ? MakeGenericClassType(args[0]) : _scope.Interpreter.GetBuiltinType(BuiltinTypeId.Type);
                case "Any":
                    return baseType;
                // TODO: Other types
                default:
                    Trace.TraceWarning("Unhandled generic: typing.{0}", baseType.Name);
                    break;
            }

            return baseType;
        }

        private IPythonType MakeSequenceType(BuiltinTypeId typeId, BuiltinTypeId iterTypeId, IReadOnlyList<IPythonType> types) {
            var res = _scope.Interpreter.GetBuiltinType(typeId);
            if (types.Count > 0) {
                var iterRes = _scope.Interpreter.GetBuiltinType(iterTypeId);
                res = new AstPythonSequence(res, _scope.Module, types.Select(Finalize), new AstPythonIterator(iterRes, types, _scope.Module));
            }
            return res;
        }

        private IPythonType MakeIterableType(IReadOnlyList<IPythonType> types) {
            var iterator = MakeIteratorType(types);
            var bti = BuiltinTypeId.List;
            switch (iterator.TypeId) {
                case BuiltinTypeId.BytesIterator:
                    bti = BuiltinTypeId.Bytes;
                    break;
                case BuiltinTypeId.UnicodeIterator:
                    bti = BuiltinTypeId.Unicode;
                    break;
            }

            return new AstPythonIterable(_scope.Interpreter.GetBuiltinType(bti), types, iterator, _scope.Module);
        }

        private IPythonType MakeIteratorType(IReadOnlyList<IPythonType> types) {
            var bti = BuiltinTypeId.ListIterator;
            if (types.Any(t => t.TypeId == BuiltinTypeId.Bytes)) {
                bti = BuiltinTypeId.BytesIterator;
            } else if (types.Any(t => t.TypeId == BuiltinTypeId.Unicode)) {
                bti = BuiltinTypeId.UnicodeIterator;
            }

            return new AstPythonIterator(_scope.Interpreter.GetBuiltinType(bti), types, _scope.Module);
        }

        private IPythonType MakeLookupType(BuiltinTypeId typeId, IReadOnlyList<IPythonType> types) {
            var res = _scope.Interpreter.GetBuiltinType(typeId);
            if (types.Count > 0) {
                var keys = FinalizeList(types.ElementAtOrDefault(0));
                res = new AstPythonLookup(
                    res,
                    _scope.Module,
                    keys,
                    FinalizeList(types.ElementAtOrDefault(1)),
                    null,
                    new AstPythonIterator(_scope.Interpreter.GetBuiltinType(BuiltinTypeId.DictKeys), keys, _scope.Module)
                );
            }
            return res;
        }

        private IPythonType MakeGenericClassType(IPythonType typeArg) {
            if (typeArg.IsBuiltin) {
                if (_scope.Interpreter.GetBuiltinType(typeArg.TypeId) is AstPythonType type) {
                    return type.TypeId == BuiltinTypeId.Unknown
                        ? _scope.Interpreter.GetBuiltinType(BuiltinTypeId.Type)
                        : type.GetTypeFactory();
                }
            }
            return new AstPythonType(typeArg.Name, _scope.Module, typeArg.Documentation, null, BuiltinTypeId.Type, isTypeFactory: true);
        }

        private class ModuleType : AstPythonType {
            public ModuleType(IPythonModule module):
                base(module.Name, module, module.Documentation, null) {
             }

            public override BuiltinTypeId TypeId => BuiltinTypeId.Module;
            public override PythonMemberType MemberType => PythonMemberType.Module;

            public override  IMember GetMember(IModuleContext context, string name) => DeclaringModule.GetMember(context, name);
            public override IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => DeclaringModule.GetMemberNames(moduleContext);
        }

        private class UnionType : AstPythonType, IPythonMultipleMembers {
            public UnionType(IReadOnlyList<IPythonType> types):
                base("Any", types.Select(t => t.DeclaringModule).ExcludeDefault().FirstOrDefault(), null, null) {
                Types = types;
            }

            public IReadOnlyList<IPythonType> Types { get; }

            public IReadOnlyList<IMember> GetMembers() => Types.OfType<IMember>().ToArray();

            public override IMember GetMember(IModuleContext context, string name) => new UnionType(
                Types.Select(t => t.GetMember(context, name)).OfType<IPythonType>().ToArray()
            );

            public override IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Types.SelectMany(t => t.GetMemberNames(moduleContext));
        }

        private class NameType : AstPythonType {
            public NameType(string name): base(name, BuiltinTypeId.Unknown) { }
         }
    }
}
