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
using System.Text;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// An analysis value represents a set of variables and code.  Examples of 
    /// analysis values include top-level code, classes, and functions.
    /// </summary>
    public class AnalysisValue : IAnalysisValueOperations {
        public const int MaxShortDescriptionLength = 128;

        [ThreadStatic]
        private static HashSet<AnalysisValue> _processing;
        private string _shortDescription;

        protected AnalysisValue() { }


        public virtual bool IsAlive => DeclaringModule == null || DeclaringVersion == DeclaringModule.AnalysisVersion;

        bool ICanExpire.IsAlive => IsAlive;

        /// <summary>
        /// Returns an immutable set which contains just this AnalysisValue.
        /// 
        /// Currently implemented as returning the AnalysisValue object directly which implements ISet{AnalysisValue}.
        /// </summary>
        public IAnalysisSet SelfSet => this;

        /// <summary>
        /// Gets the name of the value if it has one, or null if it's a non-named item.
        /// 
        /// The name property here is typically the same value you'd get by accessing __name__
        /// on the real Python object.
        /// </summary>
        public virtual string Name => null;

        /// <summary>
        /// Gets the documentation of the value.
        /// </summary>
        public virtual string Documentation => null;

        /// <summary>
        /// Gets a list of locations where this value is defined.
        /// </summary>
        public virtual IEnumerable<ILocationInfo> Locations => LocationInfo.Empty;

        public virtual IEnumerable<OverloadResult> Overloads => Enumerable.Empty<OverloadResult>();

        public virtual string Description
            => this is IHasRichDescription hrd
                ? string.Join("", hrd.GetRichDescription().Select(kv => kv.Value))
                : string.Empty;

        public virtual string ShortDescription {
            get {
                if (string.IsNullOrEmpty(_shortDescription) && this is IHasRichDescription hrd) {
                    var sb = new StringBuilder();
                    foreach (var item in hrd.GetRichDescription().TakeWhile(kv => kv.Key != WellKnownRichDescriptionKinds.EndOfDeclaration).Select(kv => kv.Value)) {
                        if (sb.Length >= MaxShortDescriptionLength) {
                            sb.Append("...");
                            break;
                        }
                        sb.Append(item);
                    }
                    _shortDescription = sb.ToString();
                }
                return _shortDescription ?? Description;
            }
        }

        /// <summary>
        /// Returns the member type of the analysis value, or PythonMemberType.Unknown if it's unknown.
        /// </summary>
        public virtual PythonMemberType MemberType => PythonMemberType.Unknown;

        public virtual IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None)
            => new Dictionary<string, IAnalysisSet>();

        /// <summary>
        /// Gets the constant value that this object represents, if it's a constant.
        /// 
        /// Returns Type.Missing if the value is not constant (because it returns null
        /// if the type is None).
        /// </summary>
        /// <returns></returns>
        public virtual object GetConstantValue() => Type.Missing;

        /// <summary>
        /// Returns the constant value as a string.  This returns a string if the constant
        /// value is either a unicode or ASCII string.
        /// </summary>
        public string GetConstantValueAsString() {
            var constName = GetConstantValue();
            if (constName != null) {
                string unicodeName = constName as string;
                AsciiString asciiName;
                if (unicodeName != null) {
                    return unicodeName;
                } else if ((asciiName = constName as AsciiString) != null) {
                    return asciiName.String;
                }
            }
            return null;
        }

        public virtual IPythonType PythonType => null;
        public virtual IPythonProjectEntry DeclaringModule => null;
        public virtual int DeclaringVersion => -1;

        public virtual IMro Mro => Values.Mro.Empty;

        public virtual AnalysisUnit AnalysisUnit => null;
        IAnalysisUnit IAnalysisValue.AnalysisUnit => AnalysisUnit;

        #region Dynamic Operations

        /// <summary>
        /// Attempts to call this object and returns the set of possible types it can return.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="args">The arguments being passed to the function</param>
        /// <param name="keywordArgNames">Keyword argument names, * and ** are included in here for splatting calls</param>
        public virtual IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) => AnalysisSet.Empty;

        /// <summary>
        /// Attempts to get a member from this object with the specified name.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="name">The name of the member.</param>
        /// <remarks>
        /// Overrides of this method must unconditionally call the base
        /// implementation, even if the return value is ignored.
        /// </remarks>
        public virtual IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) => GetTypeMember(node, unit, name);

        /// <summary>
        /// Gets an attribute that's only declared in the classes dictionary, not in an instance
        /// dictionary
        /// </summary>
        public virtual IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) => AnalysisSet.Empty;

        public virtual void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
        }

        public virtual void DeleteMember(Node node, AnalysisUnit unit, string name) {
        }

        public virtual void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
        }

        public virtual IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) => CallReverseBinaryOp(node, unit, operation, rhs);

        public IAnalysisSet CallReverseBinaryOp(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            switch (operation) {
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                case PythonOperator.In:
                case PythonOperator.NotIn:
                    return unit.DeclaringModule.ProjectEntry.ProjectState.ClassInfos[BuiltinTypeId.Bool].Instance;
                default:
                    var res = AnalysisSet.Empty;
                    foreach (var value in rhs) {
                        res = res.Union(value.ReverseBinaryOperation(node, unit, operation, SelfSet));
                    }

                    return res;
            }
        }

        /// <summary>
        /// Provides implementation of __r*__methods (__radd__, __rsub__, etc...)
        /// 
        /// This is dispatched to when the LHS doesn't understand the RHS.  Unlike normal Python it's currently
        /// the LHS responsibility to dispatch to this.
        /// </summary>
        public virtual IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) => AnalysisSet.Empty;

        public virtual IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) => this.SelfSet;

        /// <summary>
        /// Returns the length of the object if it's known, or null if it's not a fixed size object.
        /// </summary>
        /// <returns></returns>
        public virtual int? GetLength() => null;

        public virtual IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) =>
            // TODO: need more than constant 0...
            //index = (VariableRef(ConstantInfo(0, self.ProjectState, False)), )
            //self.AssignTo(self._state.IndexInto(listRefs, index), node, node.Left)
            GetIndex(node, unit, unit.State.ClassInfos[BuiltinTypeId.Int].SelfSet);

        public virtual IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) => AnalysisSet.Empty;

        public virtual IAnalysisSet GetIterator(Node node, AnalysisUnit unit) => GetTypeMember(node, unit, "__iter__").Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames);

        public virtual IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit)
            => GetTypeMember(node, unit, "__aiter__")
                .Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames)
                .Await(node, unit);

        public virtual IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index)
            => GetTypeMember(node, unit, "__getitem__").Call(node, unit, new[] { index }, ExpressionEvaluator.EmptyNames);

        /// <summary>
        /// Returns a list of key/value pairs stored in the this object which are retrivable using
        /// indexing.  For lists the key values will be integers (potentially constant, potentially not), 
        /// for dicts the key values will be arbitrary analysis values.
        /// </summary>
        public virtual IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            yield break;
        }

        public virtual void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
        }

        public virtual IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) => SelfSet;

        public virtual IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context) => SelfSet;

        public virtual IAnalysisSet GetInstanceType() => SelfSet;

        public virtual IAnalysisSet Await(Node node, AnalysisUnit unit) => AnalysisSet.Empty;

        public virtual IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit) => AnalysisSet.Empty;

        public virtual bool IsOfType(IAnalysisSet klass) => false;

        public virtual BuiltinTypeId TypeId => BuiltinTypeId.Unknown;

        /// <summary>
        /// If required, returns the resolved version of this value. If there is nothing
        /// to resolve, returns <c>this</c>.
        /// </summary>
        public IAnalysisSet Resolve(AnalysisUnit unit) => Resolve(unit, ResolutionContext.Complete);

        /// <summary>
        /// If required, returns the resolved version of this value given a specific context.
        /// </summary>
        /// <remarks>
        internal virtual IAnalysisSet Resolve(AnalysisUnit unit, ResolutionContext context) => this;

        #endregion

        #region Union Equality

        /// <summary>
        /// Returns an analysis value representative of both this and another
        /// analysis value. This should only be called when
        /// <see cref="UnionEquals"/> returns true for the two values.
        /// </summary>
        /// <param name="av">The value to merge with.</param>
        /// <param name="strength">A value matching that passed to
        /// <see cref="UnionEquals"/>.</param>
        /// <returns>A merged analysis value.</returns>
        /// <remarks>
        /// <para>Calling this function when <see cref="UnionEquals"/> returns
        /// false for the same parameters is undefined.</para>
        /// 
        /// <para>Where there is no analysis value representative of those
        /// provided, it is preferable to return this rather than
        /// <paramref name="av"/>.</para>
        /// 
        /// <para>
        /// <paramref name="strength"/> is used as a key in this function and must
        /// match the value used in <see cref="UnionEquals"/>.
        /// </para>
        /// </remarks>
        internal virtual AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) => this;

        /// <summary>
        /// Determines whether two analysis values are effectively equivalent.
        /// </summary>
        /// <remarks>
        /// The intent of <paramref name="strength"/> is to allow different
        /// types to merge more aggressively. For example, string constants
        /// may merge into a non-specific string instance at a low strength,
        /// while distinct user-defined types may merge into <c>object</c> only
        /// at higher strengths. There is no defined maximum value.
        /// </remarks>
        internal virtual bool UnionEquals(AnalysisValue av, int strength) => Equals(av);

        /// <summary>
        /// Returns a hash code for this analysis value for the given strength.
        /// </summary>
        /// <remarks>
        /// <paramref name="strength"/> must match the value that will be
        /// passed to <see cref="UnionEquals"/> and
        /// <see cref="UnionMergeTypes"/> to ensure valid results.
        /// </remarks>
        internal virtual int UnionHashCode(int strength) => GetHashCode();

        #endregion

        #region Recursion Tracking

        /// <summary>
        /// Tracks whether or not we're currently processing this value to
        /// prevent stack overflows. Returns true if the the variable should be
        /// processed.
        /// </summary>
        /// <returns>
        /// True if the variable should be processed. False if it should be
        /// skipped.
        /// </returns>
        public bool Push() {
            _processing = _processing ?? new HashSet<AnalysisValue>();
            return _processing.Add(this);
        }

        public void Pop() {
            bool wasRemoved = _processing.Remove(this);
            Debug.Assert(wasRemoved, $"Popped {GetType().FullName} but it wasn't pushed");
        }

        #endregion



        internal virtual void AddReference(Node node, AnalysisUnit analysisUnit) {
        }

        internal virtual IEnumerable<ILocationInfo> References {
            get {
                yield break;
            }
        }

        public override string ToString() => ShortDescription;

        IAnalysisSet IAnalysisSet.Add(AnalysisValue item, bool canMutate) {
            if (((IAnalysisSet)this).Comparer.Equals(this, item)) {
                return this;
            }
            return new AnalysisSetDetails.AnalysisSetTwoObject(this, item);
        }

        IAnalysisSet IAnalysisSet.Add(AnalysisValue item, out bool wasChanged, bool canMutate) {
            if (((IAnalysisSet)this).Comparer.Equals(this, item)) {
                wasChanged = false;
                return this;
            }
            wasChanged = true;
            return new AnalysisSetDetails.AnalysisSetTwoObject(this, item);
        }

        IAnalysisSet IAnalysisSet.Union(IEnumerable<AnalysisValue> items, bool canMutate) {
            if (items == null || items.All(av => ((IAnalysisSet)this).Comparer.Equals(this, av))) {
                return this;
            }
            return AnalysisSet.Create(items, ((IAnalysisSet)this).Comparer).Add(this, false);
        }

        IAnalysisSet IAnalysisSet.Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate) {
            if (items.All(av => ((IAnalysisSet)this).Comparer.Equals(this, av))) {
                wasChanged = false;
                return this;
            }
            return AnalysisSet.Create(items, ((IAnalysisSet)this).Comparer).Add(this, out wasChanged, false);
        }

        IAnalysisSet IAnalysisSet.Clone() => this;

        bool IAnalysisSet.Contains(AnalysisValue item) => ((IAnalysisSet)this).Comparer.Equals(this, item);

        bool IAnalysisSet.SetEquals(IAnalysisSet other) {
            if (other.Count != 1) {
                return false;
            }
            var av = other as AnalysisValue;
            if (av != null) {
                return ((IAnalysisSet)this).Comparer.Equals(this, av);
            }

            return ((IAnalysisSet)this).Comparer.Equals(this, other.First());
        }

        int IAnalysisSet.Count => 1;

        IEqualityComparer<AnalysisValue> IAnalysisSet.Comparer => ObjectComparer.Instance;

        IEnumerator<AnalysisValue> IEnumerable<AnalysisValue>.GetEnumerator() {
            yield return this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => ((IEnumerable<AnalysisValue>)this).GetEnumerator();
    }
}
