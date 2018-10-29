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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    abstract class Protocol : AnalysisValue, IHasRichDescription {
        private Dictionary<string, IAnalysisSet> _members;

        public Protocol(ProtocolInfo self) {
            Self = self;
        }

        public ProtocolInfo Self { get; private set; }

        public virtual Protocol Clone(ProtocolInfo newSelf) {
            var p = ((Protocol)MemberwiseClone());
            p._members = null;
            p.Self = Self;
            return p;
        }

        protected void EnsureMembers() {
            if (_members == null) {
                var m = new Dictionary<string, IAnalysisSet>();
                EnsureMembers(m);
                _members = m;
            }
        }

        protected virtual void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
        }

        protected IAnalysisSet MakeMethod(string qualname, IAnalysisSet returnValue) {
            return MakeMethod(qualname, Array.Empty<IAnalysisSet>(), returnValue);
        }

        protected IAnalysisSet MakeMethod(string qualname, IReadOnlyList<IAnalysisSet> arguments, IAnalysisSet returnValue) {
            var v = new ProtocolInfo(Self.DeclaringModule, Self.State);
            v.AddProtocol(new CallableProtocol(v, qualname, arguments, returnValue, PythonMemberType.Method));
            return v;
        }

        public override PythonMemberType MemberType => PythonMemberType.Unknown;

        // Do not return any default values from protocols. We call these directly and handle null.
        public override IAnalysisSet Await(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) => null;
        public override IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) => null;
        public override IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context) => null;
        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) => null;
        public override IAnalysisSet GetInstanceType() => null;
        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() => null;
        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) => null;
        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) => null;

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            EnsureMembers();
            return _members;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            EnsureMembers();
            if (_members.TryGetValue(name, out var m)) {
                return (m as Protocol)?.GetMember(node, unit, name) ?? m;
            }
            return res;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            base.SetMember(node, unit, name, value);
            EnsureMembers();
            if (_members.TryGetValue(name, out var m)) {
                (m as Protocol)?.SetMember(node, unit, name, value);
            }
        }

        public virtual IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
        }

        protected abstract bool Equals(Protocol other);

        public override bool Equals(object obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (obj is Protocol other && GetType() == other.GetType()) {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode() => GetType().GetHashCode();

        protected virtual bool UnionEquals(Protocol p) => Equals(p);
        protected virtual Protocol UnionMergeTypes(Protocol p) => this;

        internal sealed override bool UnionEquals(AnalysisValue av, int strength) => av is Protocol p && GetType() == p.GetType() && UnionEquals(p);
        internal override int UnionHashCode(int strength) => GetHashCode();
        internal sealed override AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) => av is Protocol p ? UnionMergeTypes(p) : this;
    }

    class NameProtocol : Protocol {
        private readonly string _name, _doc;
        private readonly BuiltinTypeId _typeId;
        private List<KeyValuePair<string, string>> _richDescription;

        public NameProtocol(ProtocolInfo self, string name, string documentation = null, BuiltinTypeId typeId = BuiltinTypeId.Unknown, PythonMemberType memberType = PythonMemberType.Unknown) : base(self) {
            _name = name;
            _doc = documentation;
            _typeId = typeId;
            MemberType = memberType;
            _richDescription = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, _name) };
        }

        public NameProtocol(ProtocolInfo self, IPythonType type) : base(self) {
            _name = type.Name;
            _doc = type.Documentation;
            _typeId = type.TypeId;
            MemberType = type.MemberType;
            _richDescription = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, _name) };
        }

        public void ExtendDescription(KeyValuePair<string, string> part) {
            _richDescription.Add(part);
        }

        public void ExtendDescription(IEnumerable<KeyValuePair<string, string>> parts) {
            _richDescription.AddRange(parts);
        }

        public override string Name => _name;
        public override string Documentation => _doc;
        public override BuiltinTypeId TypeId => _typeId;
        public override PythonMemberType MemberType { get; }
        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() => _richDescription;

        protected override bool Equals(Protocol other) => Name == other.Name;
        public override int GetHashCode() => new { Type = GetType(), Name }.GetHashCode();
    }

    class CallableProtocol : Protocol {
        private readonly Lazy<OverloadResult[]> _overloads;

        public CallableProtocol(ProtocolInfo self, string qualname, IReadOnlyList<IAnalysisSet> arguments, IAnalysisSet returnType, PythonMemberType memberType = PythonMemberType.Function)
            : base(self) {
            Name = qualname ?? "callable";
            Arguments = arguments;
            ReturnType = returnType.AsUnion(1);
            _overloads = new Lazy<OverloadResult[]>(GenerateOverloads);
            MemberType = memberType;
        }

        public override string Name { get; }

        public override BuiltinTypeId TypeId => BuiltinTypeId.Function;
        public override PythonMemberType MemberType { get; }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members["__call__"] = Self;
        }

        private OverloadResult[] GenerateOverloads() {
            return new[] {
                new OverloadResult(Arguments.Select(ToParameterResult).ToArray(), Name, null, ReturnType.GetShortDescriptions())
            };
        }

        private ParameterResult ToParameterResult(IAnalysisSet set, int i) {
            return new ParameterResult("${0}".FormatInvariant(i + 1), "Parameter {0}".FormatUI(i + 1), string.Join(", ", set.GetShortDescriptions()));
        }

        public IReadOnlyList<IAnalysisSet> Arguments { get; }
        public IAnalysisSet ReturnType { get; }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var def = base.Call(node, unit, args, keywordArgNames);
            return ReturnType ?? def;
        }

        public override IEnumerable<OverloadResult> Overloads => _overloads.Value;

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "(");
            int argNumber = 1;
            foreach (var a in Arguments) {
                if (argNumber > 1) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }

                foreach (var kv in a.GetRichDescriptions(defaultIfEmpty: "Any")) {
                    yield return kv;
                }
                argNumber += 1;
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")");

            foreach (var kv in ReturnType.GetRichDescriptions(" -> ")) {
                yield return kv;
            }
        }

        protected override bool Equals(Protocol other) =>
            Name == other.Name &&
            other is CallableProtocol cp &&
            Arguments.Zip(cp.Arguments, (x, y) => x.SetEquals(y)).All(b => b);
        public override int GetHashCode() => Name.GetHashCode();
    }

    class IterableProtocol : Protocol {
        protected readonly IAnalysisSet _iterator;
        protected readonly IAnalysisSet _yielded;

        public IterableProtocol(ProtocolInfo self, IAnalysisSet yielded) : base(self) {
            _yielded = yielded.AsUnion(1);

            var iterator = new ProtocolInfo(Self.DeclaringModule, Self.State);
            iterator.AddProtocol(new IteratorProtocol(iterator, _yielded));
            _iterator = iterator;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members["__iter__"] = MakeMethod("__iter__", _iterator);
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) => _iterator;
        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) => _yielded;

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                foreach (var kv in _yielded.GetRichDescriptions()) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        // Types are already checked for an exact match
        protected override bool Equals(Protocol other) => other is IterableProtocol ip && _yielded.SetEquals(ip._yielded);
        protected override bool UnionEquals(Protocol p) => true;

        protected override Protocol UnionMergeTypes(Protocol p) {
            if (p is IterableProtocol ip) {
                var yielded = _yielded.Union(ip._yielded, out bool changed, canMutate: false);
                if (changed && !yielded.SetEquals(_yielded)) {
                    return new IterableProtocol(Self, yielded);
                }
            }
            return this;
        }
    }

    class IteratorProtocol : Protocol {
        protected readonly IAnalysisSet _yielded;

        public IteratorProtocol(ProtocolInfo self, IAnalysisSet yielded) : base(self) {
            _yielded = yielded.AsUnion(1);
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            if (Self.DeclaringModule?.Tree?.LanguageVersion.Is3x() ?? true) {
                members["__next__"] = MakeMethod("__next__", _yielded);
            } else {
                members["next"] = MakeMethod("next", _yielded);
            }
            members["__iter__"] = MakeMethod("__iter__", Self);
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return _yielded;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                foreach (var kv in _yielded.GetRichDescriptions()) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        // Types are already checked for an exact match
        protected override bool Equals(Protocol other) => other is IteratorProtocol ip && _yielded.SetEquals(ip._yielded);
        protected override bool UnionEquals(Protocol p) => true;

        protected override Protocol UnionMergeTypes(Protocol p) {
            if (p is IteratorProtocol ip) {
                var yielded = _yielded.Union(ip._yielded, out bool changed);
                if (changed) {
                    return new IteratorProtocol(Self, yielded);
                }
            }
            return this;
        }
    }

    class GetItemProtocol : Protocol {
        private readonly IAnalysisSet _keyType, _valueType;

        public GetItemProtocol(ProtocolInfo self, IAnalysisSet keys, IAnalysisSet values) : base(self) {
            _keyType = (keys ?? self.AnalysisUnit.State.ClassInfos[BuiltinTypeId.Int].Instance).AsUnion(1);
            _valueType = values.AsUnion(1);
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members["__getitem__"] = MakeMethod("__getitem__", _valueType);
        }

        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            yield return new KeyValuePair<IAnalysisSet, IAnalysisSet>(_keyType, _valueType);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            if (index.IsObjectOrUnknown() || _keyType.ContainsAny(index)) {
                return _valueType;
            }
            return base.GetIndex(node, unit, index);
        }

        public override string Name => "container";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_valueType.IsObjectOrUnknownOrNone()) {
                yield break;
            }

            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
            if (_keyType.Any(k => k.TypeId != BuiltinTypeId.Int)) {
                foreach (var kv in _keyType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
            }
            foreach (var kv in _valueType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                yield return kv;
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
        }

        protected override bool Equals(Protocol other) =>
            other is GetItemProtocol gip &&
            _keyType.SetEquals(gip._keyType) &&
            _valueType.SetEquals(gip._valueType);

        // Types are already checked for an exact match
        protected override bool UnionEquals(Protocol p) => true;

        protected override Protocol UnionMergeTypes(Protocol p) {
            if (p is GetItemProtocol gip) {
                var keyType = _keyType.Union(gip._keyType, out bool changed1);
                var valueType = _valueType.Union(gip._valueType, out bool changed2);
                if (changed1 || changed2) {
                    return new GetItemProtocol(Self, keyType, valueType);
                }
            }
            return this;
        }
    }

    class TupleProtocol : IterableProtocol {
        private readonly IAnalysisSet[] _values;

        public TupleProtocol(ProtocolInfo self, IEnumerable<IAnalysisSet> values) : base(self, AnalysisSet.UnionAll(values)) {
            _values = values.Select(s => s.AsUnion(1)).ToArray();
            Name = GetNameFromValues();
        }

        private string GetNameFromValues() {
            // Enumerate manually since SelectMany drops empty/unknown values
            var sb = new StringBuilder("tuple[");
            for (var i = 0; i < _values.Length; i++) {
               sb.AppendIf(i > 0, ", ");
               AppendParameterString(sb, _values[i].ToArray());
            }
            sb.Append(']');
            return sb.ToString();
        }

        private void AppendParameterString(StringBuilder sb, AnalysisValue[] sets) {
            if (sets.Length == 0) {
                sb.Append('?');
                return;
            }

            sb.AppendIf(sets.Length > 1, "[");
            for (var i = 0; i < sets.Length; i++) {
                sb.AppendIf(i > 0, ", ");
                sb.Append(sets[i] is IHasQualifiedName qn ? qn.FullyQualifiedName : sets[i].ShortDescription);
            }
            sb.AppendIf(sets.Length > 1, "]");
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            var intType = Self.State.ClassInfos[BuiltinTypeId.Int].GetInstanceType();
            members["__getitem__"] = MakeMethod("__getitem__", new[] { intType }, _yielded);
        }

        private IAnalysisSet GetItem(int index) {
            if (index < 0) {
                index += _values.Length;
            }
            if (index >= 0 && index < _values.Length) {
                return _values[index];
            }
            return AnalysisSet.Empty;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            var constants = index.OfType<ConstantInfo>().Select(ci => ci.Value).OfType<int>().ToArray();
            if (constants.Length == 0) {
                return AnalysisSet.UnionAll(_values);
            }

            return AnalysisSet.UnionAll(constants.Select(GetItem));
        }

        public override string Name { get; }
        public override BuiltinTypeId TypeId => BuiltinTypeId.Tuple;

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_values.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                bool needComma = false;
                foreach (var v in _values) {
                    if (needComma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    needComma = true;
                    foreach (var kv in v.GetRichDescriptions()) {
                        yield return kv;
                    }
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) =>
            other is TupleProtocol tp &&
            _values.Zip(tp._values, (x, y) => x.SetEquals(y)).All(b => b);

        // Types are already checked for an exact match
        protected override bool UnionEquals(Protocol p) => p is TupleProtocol tp && _values.Length == tp._values.Length;

        protected override Protocol UnionMergeTypes(Protocol p) {
            if (p is TupleProtocol tp) {
                bool anyChanged = false;
                var values = _values.Zip(tp._values, (x, y) => {
                    var xy = x.Union(y, out bool changed);
                    anyChanged |= changed;
                    return xy;
                }).ToArray();
                if (anyChanged) {
                    return new TupleProtocol(Self, values);
                }
            }
            return this;
        }
    }

    class MappingProtocol : IterableProtocol {
        private readonly IAnalysisSet _keyType, _valueType, _itemType;

        public MappingProtocol(ProtocolInfo self, IAnalysisSet keys, IAnalysisSet values, IAnalysisSet items) : base(self, keys) {
            _keyType = keys.AsUnion(1);
            _valueType = values.AsUnion(1);
            _itemType = items.AsUnion(1);
        }

        private IAnalysisSet MakeIterable(IAnalysisSet values) {
            var pi = new ProtocolInfo(DeclaringModule, Self.State);
            pi.AddProtocol(new IterableProtocol(pi, values));
            return pi;
        }

        private IAnalysisSet MakeView(IPythonType type, IAnalysisSet values) {
            var pi = new ProtocolInfo(DeclaringModule, Self.State);
            var np = new NameProtocol(pi, type);
            var ip = new IterableProtocol(pi, values);
            np.ExtendDescription(ip.GetRichDescription());
            pi.AddProtocol(np);
            pi.AddProtocol(ip);
            return pi;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            var state = Self.State;
            var itemsIter = MakeIterable(_itemType);

            if (state.LanguageVersion.Is3x()) {
                members["keys"] = MakeMethod("keys", MakeView(state.Types[BuiltinTypeId.DictKeys], _keyType));
                members["values"] = MakeMethod("values", MakeView(state.Types[BuiltinTypeId.DictValues], _valueType));
                members["items"] = MakeMethod("items", MakeView(state.Types[BuiltinTypeId.DictItems], _itemType));
            } else {
                members["viewkeys"] = MakeMethod("viewkeys", MakeView(state.Types[BuiltinTypeId.DictKeys], _keyType));
                members["viewvalues"] = MakeMethod("viewvalues", MakeView(state.Types[BuiltinTypeId.DictValues], _valueType));
                members["viewitems"] = MakeMethod("viewitems", MakeView(state.Types[BuiltinTypeId.DictItems], _itemType));
                var keysIter = MakeIterable(_keyType);
                members["keys"] = MakeMethod("keys", keysIter);
                members["iterkeys"] = MakeMethod("iterkeys", keysIter);
                var valuesIter = MakeIterable(_valueType);
                members["values"] = MakeMethod("values", valuesIter);
                members["itervalues"] = MakeMethod("itervalues", valuesIter);
                members["items"] = MakeMethod("items", itemsIter);
                members["iteritems"] = MakeMethod("iteritems", itemsIter);
            }

            members["clear"] = MakeMethod("clear", AnalysisSet.Empty);
            members["get"] = MakeMethod("get", new[] { _keyType }, _valueType);
            members["pop"] = MakeMethod("pop", new[] { _keyType }, _valueType);
            members["popitem"] = MakeMethod("popitem", new[] { _keyType }, _itemType);
            members["setdefault"] = MakeMethod("setdefault", new[] { _keyType, _valueType }, _valueType);
            members["update"] = MakeMethod("update", new[] { AnalysisSet.UnionAll(new IAnalysisSet[] { this, itemsIter }) }, AnalysisSet.Empty);
        }

        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            yield return new KeyValuePair<IAnalysisSet, IAnalysisSet>(_keyType, _valueType);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            return _valueType;
        }

        public override string Name => "dict";
        public override BuiltinTypeId TypeId => BuiltinTypeId.Dict;

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_valueType.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                if (_keyType.Any(k => k.TypeId != BuiltinTypeId.Int)) {
                    foreach (var kv in _keyType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                foreach (var kv in _valueType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) =>
            other is MappingProtocol mp &&
            _keyType.SetEquals(mp._keyType) &&
            _valueType.SetEquals(mp._valueType);

        // Types are already checked for an exact match
        protected override bool UnionEquals(Protocol p) => true;

        protected override Protocol UnionMergeTypes(Protocol p) {
            if (p is MappingProtocol mp) {
                var keyType = _keyType.Union(mp._keyType, out bool changed1);
                var valueType = _valueType.Union(mp._valueType, out bool changed2);
                var itemType = _itemType.Union(mp._itemType, out bool changed3);
                if (changed1 || changed2 || changed3) {
                    return new MappingProtocol(Self, keyType, valueType, itemType);
                }
            }
            return this;
        }
    }

    class GeneratorProtocol : IteratorProtocol {
        public GeneratorProtocol(ProtocolInfo self, IAnalysisSet yields, IAnalysisSet sends, IAnalysisSet returns) : base(self, yields) {
            Sent = sends.AsUnion(1);
            Returned = returns.AsUnion(1);
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            base.EnsureMembers(members);

            members["send"] = MakeMethod("send", new[] { Sent }, _yielded);
            members["throw"] = MakeMethod("throw", new[] { AnalysisSet.Empty }, AnalysisSet.Empty);
        }

        public override string Name => "generator";
        public override BuiltinTypeId TypeId => BuiltinTypeId.Generator;
        
        public IAnalysisSet Yielded => _yielded;
        public IAnalysisSet Sent { get; }
        public IAnalysisSet Returned { get; }

        public override IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) => Returned;

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_yielded.Any() || Sent.Any() || Returned.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                if (_yielded.Any()) {
                    foreach (var kv in _yielded.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                } else {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[]");
                }

                if (Sent.Any()) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    foreach (var kv in Sent.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                } else if (Returned.Any()) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[]");
                }

                if (Returned.Any()) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    foreach (var kv in Sent.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                        yield return kv;
                    }
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }

        protected override bool Equals(Protocol other) =>
            other is GeneratorProtocol gp &&
            _yielded.SetEquals(gp._yielded) &&
            Sent.SetEquals(gp.Sent) &&
            Returned.SetEquals(gp.Returned);

        // Types are already checked for an exact match
        protected override bool UnionEquals(Protocol p) => true;

        protected override Protocol UnionMergeTypes(Protocol p) {
            if (p is GeneratorProtocol gp) {
                var yielded = _yielded.Union(gp._yielded, out bool changed1);
                var sent = Sent.Union(gp.Sent, out bool changed2);
                var returned = Returned.Union(gp.Returned, out bool changed3);
                if (changed1 || changed2 || changed3) {
                    return new GeneratorProtocol(Self, yielded, sent, returned);
                }
            }
            return this;
        }
    }

    class NamespaceProtocol : Protocol {
        private readonly string _name;
        private readonly VariableDef _values;

        public NamespaceProtocol(ProtocolInfo self, string name) : base(self) {
            _name = name;
            _values = new VariableDef();
        }

        public override Protocol Clone(ProtocolInfo newSelf) {
            var np = new NamespaceProtocol(newSelf, _name);
            _values.CopyTo(np._values);
            return np;
        }

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members[_name] = this;
        }

        public override string Name => _name;

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (name == _name) {
                _values.AddDependency(unit);
                return _values.Types;
            }
            return AnalysisSet.Empty;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            _values.AddTypes(unit, value);
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
            foreach (var kv in _values.Types.GetRichDescriptions(prefix: ": ", unionPrefix: "{", unionSuffix: "}")) {
                yield return kv;
            }
        }

        protected override bool Equals(Protocol other) => Name == other.Name;
        public override int GetHashCode() => new { Type = GetType(), Name }.GetHashCode();

        protected override bool UnionEquals(Protocol p) => Equals(p);
        internal override int UnionHashCode(int strength) => GetHashCode();
        protected override Protocol UnionMergeTypes(Protocol p) {
            if (p is NamespaceProtocol np) {
                np._values.MakeUnionStrongerIfMoreThan(Self.State.Limits.InstanceMembers, np._values.Types);
                np._values.CopyTo(_values);
            }
            return this;
        }
    }

    class InstanceProtocol : CallableProtocol {
        public InstanceProtocol(ProtocolInfo self, IReadOnlyList<IAnalysisSet> arguments, IAnalysisSet instance) :
            base(self, null, arguments, instance, PythonMemberType.Class) { }

        public override IAnalysisSet GetInstanceType() => ReturnType;
        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) => ReturnType;

        protected override bool Equals(Protocol other) => ReturnType.SetEquals(((InstanceProtocol)other).ReturnType);
        public override int GetHashCode() => GetType().GetHashCode();

        protected override bool UnionEquals(Protocol p) => Equals(p);
        internal override int UnionHashCode(int strength) => GetHashCode();
        protected override Protocol UnionMergeTypes(Protocol p) => this;
    }

    /// <summary>
    /// List protocol that delegates work to its inner class: List[T] has list methods.
    /// </summary>
    class ListProtocol : IterableProtocol {
        protected readonly AnalysisValue _actualType;

        public ListProtocol(ProtocolInfo self, AnalysisValue actualType, IAnalysisSet valueTypes) : base(self, valueTypes) {
            _actualType = actualType;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None)
            => _actualType.GetAllMembers(moduleContext, options);
        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) => _actualType.GetMember(node, unit, name);
        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) => _actualType.GetTypeMember(node, unit, name);
    }

    /// <summary>
    /// Dict protocol that delegates work to its inner class: Dict[T1, T2] has dict methods.
    /// </summary>
    class DictProtocol : MappingProtocol {
        protected readonly AnalysisValue _actualType;

        public DictProtocol(ProtocolInfo self, AnalysisValue actualType, IAnalysisSet keyTypes, IAnalysisSet valueTypes, IAnalysisSet items) 
            : base(self, keyTypes, valueTypes, items) {
            _actualType = actualType;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None)
            => _actualType.GetAllMembers(moduleContext, options);
        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) => _actualType.GetMember(node, unit, name);
        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) => _actualType.GetTypeMember(node, unit, name);
    }

    /// <summary>
    /// Generic protocol that delegates work to its inner class.
    /// Typically used in NewType and TypeVar scenarios delegating 
    /// calls to the actual class serving as new type.
    /// </summary>
    class TypeDelegateProtocol : Protocol {
        protected readonly AnalysisValue _actualType;

        /// <summary>
        /// Creates protocol that delegates to base type, such as generic list
        /// provides methods from the plain list.
        /// </summary>
        /// <param name="self">Protocol info</param>
        /// <param name="baseType">Base type to delegate to, such as 'list'.</param>
        public TypeDelegateProtocol(ProtocolInfo self, AnalysisValue actualType) : base(self) {
            _actualType = actualType;
        }

        protected override bool Equals(Protocol other) => _actualType.Equals((other as TypeDelegateProtocol)._actualType);

        public override string Name => _actualType.Name;
        public override string Documentation => _actualType.Documentation;
        public override string Description => _actualType.Description;
        public override string ShortDescription => _actualType.ShortDescription;
        public override BuiltinTypeId TypeId => _actualType.TypeId;
        public override PythonMemberType MemberType => _actualType.MemberType;
        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription()
            => _actualType is IHasRichDescription rd
                ? rd.GetRichDescription()
                : Enumerable.Empty<KeyValuePair<string, string>>();

        public override IPythonProjectEntry DeclaringModule => _actualType.DeclaringModule;
        public override int DeclaringVersion => _actualType.DeclaringVersion;
        public override IPythonType PythonType => _actualType.PythonType;
        public override bool IsOfType(IAnalysisSet klass) => _actualType.IsOfType(klass);
        public override bool IsAlive => _actualType.IsAlive;

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None)
            => _actualType.GetAllMembers(moduleContext, options);
        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) => _actualType.GetMember(node, unit, name);
        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) => _actualType.GetTypeMember(node, unit, name);
        public override IAnalysisSet GetInstanceType() => _actualType.GetInstanceType();

        public override AnalysisUnit AnalysisUnit => _actualType.AnalysisUnit;
        internal override void AddReference(Node node, AnalysisUnit analysisUnit) => _actualType.AddReference(node, analysisUnit);
        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value)
            => _actualType.AugmentAssign(node, unit, value);
        public override IAnalysisSet Await(Node node, AnalysisUnit unit) => _actualType.Await(node, unit);
        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs)
            => _actualType.BinaryOperation(node, unit, operation, rhs);
        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames)
            => _actualType.Call(node, unit, args, keywordArgNames);
        public override void DeleteMember(Node node, AnalysisUnit unit, string name) => _actualType.DeleteMember(node, unit, name);
        public override IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) => _actualType.GetAsyncEnumeratorTypes(node, unit);
        public override IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit) => _actualType.GetAsyncIterator(node, unit);
        public override object GetConstantValue() => _actualType.GetConstantValue();
        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit)
            => _actualType.GetDescriptor(node, instance, context, unit);

        public override IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context)
            => _actualType.GetDescriptor(projectState, instance, context);
        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) => _actualType.GetEnumeratorTypes(node, unit);
        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) => _actualType.GetIndex(node, unit, index);
        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() => _actualType.GetItems();
        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) => _actualType.GetIterator(node, unit);
        public override IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) => _actualType.GetReturnForYieldFrom(node, unit);
        public override IEnumerable<ILocationInfo> Locations => _actualType.Locations;
        public override IMro Mro => _actualType.Mro;
        public override IEnumerable<OverloadResult> Overloads => _actualType.Overloads;
        public override int? GetLength() => _actualType.GetLength();
        internal override IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) => _actualType.Resolve(unit, context);
        internal override IEnumerable<ILocationInfo> References => _actualType.References;
        public override IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs)
            => _actualType.ReverseBinaryOperation(node, unit, operation, rhs);

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) => _actualType.SetIndex(node, unit, index, value);
        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) => _actualType.UnaryOperation(node, unit, operation);
        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) => _actualType.SetMember(node, unit, name, value);
    }
}
