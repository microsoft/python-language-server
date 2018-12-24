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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class TypeAnnotationConverter : TypeAnnotationConverter<IPythonType> {
        private readonly ExpressionLookup _scope;

        public TypeAnnotationConverter(ExpressionLookup scope) {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public override IPythonType Finalize(IPythonType type) {
            if (type.IsUnknown() || type is IPythonModule) {
                return null;
            }

            var n = GetName(type);
            if (!string.IsNullOrEmpty(n)) {
                return _scope.LookupNameInScopes(n).GetPythonType();
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

        public override IPythonType LookupName(string name)
            => _scope.LookupNameInScopes(name, ExpressionLookup.LookupOptions.Global | ExpressionLookup.LookupOptions.Builtins)?.GetPythonType();

        public override IPythonType GetTypeMember(IPythonType baseType, string member) 
            => baseType.GetMember(member)?.GetPythonType();

        public override IPythonType MakeNameType(string name) => new NameType(name);
        public override string GetName(IPythonType type) => (type as NameType)?.Name;

        public override IPythonType MakeUnion(IReadOnlyList<IPythonType> types) => new UnionType(types);

        public override IReadOnlyList<IPythonType> GetUnionTypes(IPythonType type) =>
            type is UnionType unionType ? unionType.Types : null;

        public override IPythonType MakeGeneric(IPythonType baseType, IReadOnlyList<IPythonType> args) {
            if (args == null || args.Count == 0 || baseType == null) {
                return baseType;
            }
            // TODO: really handle typing

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
                    return _scope.Interpreter.GetBuiltinType(BuiltinTypeId.Str);
                case "Type":
                    // TODO: handle arguments
                    return _scope.Interpreter.GetBuiltinType(BuiltinTypeId.Type);
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
                res = new PythonSequence(res, _scope.Module, types.Select(Finalize), new PythonIterator(iterRes, types, _scope.Module));
            }
            return res;
        }

        private IPythonType MakeIterableType(IReadOnlyList<IPythonType> types) {
            var iterator = MakeIteratorType(types);
            var bti = BuiltinTypeId.List;
            switch (iterator.TypeId) {
                case BuiltinTypeId.StrIterator:
                    bti = BuiltinTypeId.Str;
                    break;
                //case BuiltinTypeId.UnicodeIterator:
                //    bti = BuiltinTypeId.Unicode;
                //    break;
            }

            return new PythonIterable(_scope.Interpreter.GetBuiltinType(bti), types, iterator, _scope.Module);
        }

        private IPythonType MakeIteratorType(IReadOnlyList<IPythonType> types) {
            var bti = BuiltinTypeId.ListIterator;
            if (types.Any(t => t.TypeId == BuiltinTypeId.Str)) {
                bti = BuiltinTypeId.StrIterator;
            }
            //} else if (types.Any(t => t.TypeId == BuiltinTypeId.Bytes)) {
            //    bti = BuiltinTypeId.BytesIterator;
            //} else if (types.Any(t => t.TypeId == BuiltinTypeId.Unicode)) {
            //    bti = BuiltinTypeId.UnicodeIterator;
            //}

            return new PythonIterator(_scope.Interpreter.GetBuiltinType(bti), types, _scope.Module);
        }

        private IPythonType MakeLookupType(BuiltinTypeId typeId, IReadOnlyList<IPythonType> types) {
            var res = _scope.Interpreter.GetBuiltinType(typeId);
            if (types.Count > 0) {
                var keys = FinalizeList(types.ElementAtOrDefault(0));
                res = new PythonLookup(
                    res,
                    _scope.Module,
                    keys,
                    FinalizeList(types.ElementAtOrDefault(1)),
                    null,
                    new PythonIterator(_scope.Interpreter.GetBuiltinType(BuiltinTypeId.DictKeys), keys, _scope.Module)
                );
            }
            return res;
        }

        private sealed class UnionType : PythonType {
            public UnionType(IReadOnlyList<IPythonType> types) :
                base("Any", types.Select(t => t.DeclaringModule).ExcludeDefault().FirstOrDefault(), null, null) {
                Types = types;
            }

            public IReadOnlyList<IPythonType> Types { get; }

            public IReadOnlyList<IPythonType> GetTypes() => Types.OfType<IPythonType>().ToArray();

            public override IMember GetMember(string name) => new UnionType(
                Types.Select(t => t.GetMember(name)).OfType<IPythonType>().ToArray()
            );

            public override IEnumerable<string> GetMemberNames() => Types.SelectMany(t => t.GetMemberNames());
        }

        private sealed class NameType : PythonType {
            public NameType(string name) : base(name, BuiltinTypeId.Unknown) { }
        }
    }
}
