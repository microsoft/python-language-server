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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Specializations.Typing {
    internal sealed class TypingModule : SpecializedModule {
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();

        private TypingModule(string modulePath, IServiceContainer services) : base("typing", modulePath, services) { }

        public static IPythonModule Create(IServiceContainer services) {
            var interpreter = services.GetService<IPythonInterpreter>();
            var module = interpreter.ModuleResolution
                .SpecializeModule("typing", modulePath => new TypingModule(modulePath, services)) as TypingModule;
            module?.SpecializeMembers();
            return module;
        }

        #region IMemberContainer
        public override IMember GetMember(string name) => _members.TryGetValue(name, out var m) ? m : null;
        public override IEnumerable<string> GetMemberNames() => _members.Keys;
        #endregion

        private void SpecializeMembers() {
            var location = new Location(this);

            _members["TypeVar"] = new TypeVar(this);
            _members["NewType"] = SpecializeNewType(location);
            _members["Type"] = new Type(this);

            _members["Iterator"] = new SpecializedGenericType("Iterator", CreateIteratorType, this);

            _members["Iterable"] = new SpecializedGenericType("Iterable", typeArgs => CreateListType("Iterable", BuiltinTypeId.List, typeArgs, false), this);
            _members["Sequence"] = new SpecializedGenericType("Sequence", typeArgs => CreateListType("Sequence", BuiltinTypeId.List, typeArgs, false), this);
            _members["MutableSequence"] = new SpecializedGenericType("MutableSequence",
                typeArgs => CreateListType("MutableSequence", BuiltinTypeId.List, typeArgs, true), this);
            _members["List"] = new SpecializedGenericType("List",
                typeArgs => CreateListType("List", BuiltinTypeId.List, typeArgs, true), this);

            _members["MappingView"] = new SpecializedGenericType("MappingView",
                typeArgs => CreateDictionary("MappingView", typeArgs, false), this);

            _members["KeysView"] = new SpecializedGenericType("KeysView", CreateKeysViewType, this);
            _members["ValuesView"] = new SpecializedGenericType("ValuesView", CreateValuesViewType, this);
            _members["ItemsView"] = new SpecializedGenericType("ItemsView", CreateItemsViewType, this);

            _members["AbstractSet"] = new SpecializedGenericType("AbstractSet",
                typeArgs => CreateListType("AbstractSet", BuiltinTypeId.Set, typeArgs, true), this);
            _members["Set"] = new SpecializedGenericType("Set",
                typeArgs => CreateListType("Set", BuiltinTypeId.Set, typeArgs, true), this);
            _members["MutableSet"] = new SpecializedGenericType("MutableSet",
                typeArgs => CreateListType("MutableSet", BuiltinTypeId.Set, typeArgs, true), this);
            _members["FrozenSet"] = new SpecializedGenericType("FrozenSet",
                typeArgs => CreateListType("FrozenSet", BuiltinTypeId.Set, typeArgs, false), this);

            _members["Tuple"] = new SpecializedGenericType("Tuple", CreateTupleType, this);

            _members["Mapping"] = new SpecializedGenericType("Mapping",
                typeArgs => CreateDictionary("Mapping", typeArgs, false), this);
            _members["MutableMapping"] = new SpecializedGenericType("MutableMapping",
                typeArgs => CreateDictionary("MutableMapping", typeArgs, true), this);
            _members["Dict"] = new SpecializedGenericType("Dict",
                typeArgs => CreateDictionary("Dict", typeArgs, true), this);
            _members["OrderedDict"] = new SpecializedGenericType("OrderedDict",
                typeArgs => CreateDictionary("OrderedDict", typeArgs, true), this);
            _members["DefaultDict"] = new SpecializedGenericType("DefaultDict",
                typeArgs => CreateDictionary("DefaultDict", typeArgs, true), this);

            _members["Union"] = new SpecializedGenericType("Union", CreateUnion, this);

            _members["Counter"] = Specialized.Function("Counter", this, GetMemberDocumentation("Counter"),
                Interpreter.GetBuiltinType(BuiltinTypeId.Int)).CreateInstance(ArgumentSet.WithoutContext);

            // TODO: make these classes that support __float__, etc per spec.
            //_members["SupportsInt"] = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
            //_members["SupportsFloat"] = Interpreter.GetBuiltinType(BuiltinTypeId.Float);
            //_members["SupportsComplex"] = Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
            //_members["SupportsBytes"] = Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
            _members["ByteString"] = Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);

            _members["NamedTuple"] = new NamedTuple(this);

            _members["Any"] = new AnyType(this);
            _members["AnyStr"] = CreateAnyStr();

            _members["Optional"] = new SpecializedGenericType("Optional", CreateOptional, this);
            _members["Type"] = new SpecializedGenericType("Type", CreateType, this);

            _members["Generic"] = new SpecializedGenericType("Generic", CreateGenericClassBase, this);
        }

        private string GetMemberDocumentation(string name)
        => base.GetMember(name)?.GetPythonType()?.Documentation;

        private IPythonType SpecializeNewType(Location location) {
            var fn = PythonFunctionType.Specialize("NewType", this, GetMemberDocumentation("NewType"));
            var o = new PythonFunctionOverload(fn, location);
            // When called, create generic parameter type. For documentation
            // use original TypeVar declaration so it appear as a tooltip.
            o.SetReturnValueProvider((interpreter, overload, args, indexSpan) => CreateTypeAlias(args));
            o.SetParameters(new[] {
                    new ParameterInfo("name", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, null),
                    new ParameterInfo("tp", Interpreter.GetBuiltinType(BuiltinTypeId.Type), ParameterKind.Normal, null),
            });
            fn.AddOverload(o);
            return fn;
        }

        private IPythonType CreateListType(string typeName, BuiltinTypeId typeId, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 1) {
                // If argument is generic type parameter then this is still a generic specification
                // except instead of 'List' as in 'from typing import List' it is a template
                // like in 'class A(Generic[T], List[T])
                return typeArgs[0] is IGenericTypeParameter
                    ? ToGenericTemplate(typeName, typeArgs, BuiltinTypeId.List)
                    : TypingTypeFactory.CreateListType(Interpreter, typeName, typeId, typeArgs[0], isMutable);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateTupleType(IReadOnlyList<IPythonType> typeArgs)
            => typeArgs.Any(a => a is IGenericTypeParameter)
                ? ToGenericTemplate("Tuple", typeArgs, BuiltinTypeId.Tuple)
                : TypingTypeFactory.CreateTupleType(Interpreter, typeArgs);

        private IPythonType CreateIteratorType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                // If argument is generic type parameter then this is still a generic specification
                return typeArgs[0] is IGenericTypeParameter
                    ? ToGenericTemplate("Iterator", typeArgs, BuiltinTypeId.ListIterator)
                    : TypingTypeFactory.CreateIteratorType(Interpreter, typeArgs[0]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateDictionary(string typeName, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 2) {
                // If argument is generic type parameter then this is still a generic specification
                return typeArgs.Any(a => a is IGenericTypeParameter)
                    ? ToGenericTemplate(typeName, typeArgs, BuiltinTypeId.Dict)
                    : TypingTypeFactory.CreateDictionary(Interpreter, typeName, typeArgs[0], typeArgs[1], isMutable);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateKeysViewType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                // If argument is generic type parameter then this is still a generic specification
                return typeArgs[0] is IGenericTypeParameter
                    ? ToGenericTemplate("KeysView", typeArgs, BuiltinTypeId.ListIterator)
                    : TypingTypeFactory.CreateKeysViewType(Interpreter, typeArgs[0]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateValuesViewType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                // If argument is generic type parameter then this is still a generic specification
                return typeArgs[0] is IGenericTypeParameter
                    ? ToGenericTemplate("ValuesView", typeArgs, BuiltinTypeId.ListIterator)
                    : TypingTypeFactory.CreateValuesViewType(Interpreter, typeArgs[0]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateItemsViewType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 2) {
                // If argument is generic type parameter then this is still a generic specification
                return typeArgs.Any(a => a is IGenericTypeParameter)
                    ? ToGenericTemplate("ItemsView", typeArgs.OfType<IGenericTypeParameter>().ToArray(), BuiltinTypeId.ListIterator)
                    : TypingTypeFactory.CreateItemsViewType(Interpreter, typeArgs[0], typeArgs[1]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateTypeAlias(IArgumentSet argSet) {
            Check.Argument(nameof(argSet), () => argSet.Arguments.Count == 2);

            if (!argSet.Errors.IsNullOrEmpty()) {
                argSet.ReportErrors();
                return Interpreter.UnknownType;
            }

            // Get name argument and make sure it is a string
            string name = null;
            var nameArg = argSet.Argument<IMember>(0);
            nameArg?.TryGetConstant(out name);

            if (name != null) {
                // Get type argument and create alias
                var tpArg = argSet.Argument<IMember>(1);
                return new TypeAlias(name, tpArg?.GetPythonType() ?? Interpreter.UnknownType);
            }

            // If user provided first argument that is not a string, give diagnostic
            if (!nameArg.IsUnknown()) {
                var eval = argSet.Eval;
                var argExpr = argSet.Arguments[0].ValueExpression;
                eval.ReportDiagnostics(
                    eval.Module?.Uri,
                    new DiagnosticsEntry(Resources.NewTypeFirstArgument,
                        eval.GetLocation(argExpr).Span,
                        Diagnostics.ErrorCodes.TypingNewTypeArguments,
                        Severity.Warning, DiagnosticSource.Analysis)
                );
            }

            return Interpreter.UnknownType;
        }

        private IPythonType CreateUnion(IReadOnlyList<IMember> typeArgs) {
            if (typeArgs.Count > 0) {
                return TypingTypeFactory.CreateUnionType(Interpreter, typeArgs.Select(a => a.GetPythonType()).ToArray(), this);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateOptional(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                return typeArgs[0];
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateType(IReadOnlyList<IPythonType> typeArgs) {
            if (typeArgs.Count == 1) {
                return TypingTypeFactory.CreateType(this, typeArgs[0]);
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType CreateAnyStr() {
            var str = Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            var bytes = Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
            var unicode = Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);

            var constraints = Interpreter.LanguageVersion.Is3x()
                ? new[] { str, bytes }
                : new[] { str, unicode };
            var docArgs = new[] { "'AnyStr'" }.Concat(constraints.Select(c => c.Name));

            var documentation = CodeFormatter.FormatSequence("TypeVar", '(', docArgs);
            return new PythonTypeWrapper("AnyStr", documentation, this, Interpreter.GetBuiltinType(BuiltinTypeId.Str));
        }

        private IPythonType CreateGenericClassBase(IReadOnlyList<IPythonType> typeArgs) {
            // Handle Generic[_T1, _T2, ...]. _T1, et al are IGenericTypeParameter from TypeVar.
            // Hold the parameter until concrete type is provided at the time of the class instantiation.
            if (typeArgs.Count > 0) {
                var typeDefs = typeArgs.OfType<IGenericTypeParameter>().ToArray();
                if (typeDefs.Length == typeArgs.Count) {
                    return new GenericClassBase(typeDefs, Interpreter);
                } else {
                    // TODO: report argument mismatch
                }
            }
            // TODO: report wrong number of arguments
            return Interpreter.UnknownType;
        }

        private IPythonType ToGenericTemplate(string typeName, IReadOnlyList<IPythonType> typeArgs, BuiltinTypeId typeId) {
            if (_members[typeName] is SpecializedGenericType gt) {
                var name = CodeFormatter.FormatSequence(typeName, '[', typeArgs);
                var qualifiedName = CodeFormatter.FormatSequence($"typing:{typeName}", '[', typeArgs.Select(t => t.QualifiedName));
                var args = typeArgs.OfType<IGenericTypeParameter>().ToList();
                return new SpecializedGenericType(name, qualifiedName, gt.SpecificTypeConstructor, this, typeId, args);
            }
            return Interpreter.UnknownType;
        }
    }
}
