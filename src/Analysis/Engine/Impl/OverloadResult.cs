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
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public class OverloadResult : IOverloadResult {
        private readonly ParameterResult[] _parameters;
        private readonly string[] _returnType;

        public OverloadResult(ParameterResult[] parameters, string name, string documentation, IEnumerable<string> returnType) {
            _parameters = parameters;
            Name = name;
            Documentation = documentation;
            _returnType = returnType.MaybeEnumerate().ToArray();
        }

        public string Name { get; }
        public virtual IReadOnlyList<string> ReturnType => _returnType;
        public virtual string Documentation { get; } = string.Empty;
        public virtual ParameterResult[] Parameters => _parameters;

        internal virtual OverloadResult WithNewParameters(ParameterResult[] newParameters)
            => new OverloadResult(newParameters, Name, Documentation, _returnType);

        internal virtual OverloadResult WithoutLeadingParameters(int skipCount = 1)
            => new OverloadResult(_parameters.Skip(skipCount).ToArray(), Name, Documentation, _returnType);

        private static string Longest(string x, string y) {
            if (x == null) {
                return y;
            } else if (y == null) {
                return x;
            }

            return x.Length > y.Length ? x : y;
        }

        private static IEnumerable<string> CommaSplit(string x) {
            if (string.IsNullOrEmpty(x)) {
                yield break;
            }

            var sb = new StringBuilder();
            var nestCount = 0;
            foreach (var c in x) {
                if (c == ',' && nestCount == 0) {
                    yield return sb.ToString().Trim();
                    sb.Clear();
                    continue;
                }

                if (c == '(' || c == '[' || c == '{') {
                    nestCount += 1;
                } else if (c == ')' || c == ']' || c == '}') {
                    nestCount -= 1;
                }
                sb.Append(c);
            }

            if (sb.Length > 0) {
                yield return sb.ToString().Trim();
            }
        }

        private static string Merge(string x, string y)
            => string.Join(", ", CommaSplit(x).Concat(CommaSplit(y)).OrderBy(n => n).Distinct());

        public static OverloadResult Merge(IEnumerable<OverloadResult> overloads) {
            overloads = overloads.ToArray();

            var name = overloads.Select(o => o.Name).OrderByDescending(n => n?.Length ?? 0).FirstOrDefault();
            var doc = overloads.Select(o => o.Documentation).OrderByDescending(n => n?.Length ?? 0).FirstOrDefault();
            var parameters = overloads.Select(o => o.Parameters).Aggregate(Array.Empty<ParameterResult>(), (all, pms) => {
                var res = all.Concat(pms.Skip(all.Length)).ToArray();

                for (var i = 0; i < res.Length; ++i) {
                    if (res[i] == null) {
                        res[i] = pms[i];
                    } else {
                        var l = res[i];
                        var r = pms[i];
                        res[i] = new ParameterResult(
                            Longest(l.Name, r.Name),
                            Longest(l.Documentation, r.Documentation),
                            Merge(l.Type, r.Type),
                            l.IsOptional || r.IsOptional,
                            l.Variables?.Concat(r.Variables.MaybeEnumerate()).ToArray() ?? r.Variables,
                            Longest(l.DefaultValue, r.DefaultValue)
                        );
                    }
                }

                return res;
            });

            var returnType = overloads.SelectMany(o => o.ReturnType).Distinct();
            return new OverloadResult(parameters, name, doc, returnType);
        }

        public override string ToString() {
            var parameters = string.Join(", ",
                Parameters.Select(p => "{0}{1}:{2}{3}{4}"
                    .FormatInvariant(p.Name,
                                     p.IsOptional ? "?" : string.Empty,
                                     p.Type ?? string.Empty,
                                     p.DefaultValue != null ? "=" : string.Empty,
                                     p.DefaultValue ?? string.Empty))
            ).TrimEnd();

            var returnType = "{0}{1}{2}".FormatInvariant(
                ReturnType.Count > 1 ? "[" : string.Empty,
                ReturnType.Count > 0 ? string.Join(", ", ReturnType.OrderBy(k => k)) : "None",
                ReturnType.Count > 1 ? "]" : string.Empty
            );

            var doc = string.IsNullOrEmpty(Documentation) ? "" : "'''{0}'''".FormatInvariant(Documentation);
            return "{0}({1}) -> {2}{3}".FormatInvariant(Name, parameters, returnType, doc);
        }

        public override bool Equals(object obj) {
            if (obj is OverloadResult other) {
                return OverloadResultComparer.Instance.Equals(this, other);
            }
            return base.Equals(obj);
        }
        public override int GetHashCode() => OverloadResultComparer.Instance.GetHashCode(this);
    }

    class AccumulatedOverloadResult {
        private string _name;
        private string _doc;
        private string[] _pnames;
        private string[] _ptypes;
        private string[] _pdefaults;
        private readonly HashSet<string> _rtypes;

        public AccumulatedOverloadResult(string name, string documentation, int parameters) {
            _name = name;
            _doc = documentation ?? string.Empty;
            _pnames = new string[parameters];
            _ptypes = new string[parameters];
            _pdefaults = new string[parameters];
            ParameterCount = parameters;
            _rtypes = new HashSet<string>();
        }

        public int ParameterCount { get; }

        private bool AreNullOrEqual(string x, string y) =>
            string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y) || string.Equals(x, y, StringComparison.Ordinal);

        private bool AreNullOrEqual(IAnalysisSet x, IAnalysisSet y) {
            return x == null || x.IsObjectOrUnknown() ||
                y == null || y.IsObjectOrUnknown() ||
                x.SetEquals(y);
        }

        private string ChooseBest(string x, string y) {
            if (string.IsNullOrEmpty(x)) {
                return string.IsNullOrEmpty(y) ? null : y;
            }
            if (string.IsNullOrEmpty(y)) {
                return null;
            }
            return x.Length >= y.Length ? x : y;
        }

        public bool TryAddOverload(string name, string documentation, string[] names, string[] types, string[] defaults, IEnumerable<string> returnTypes) {
            if (names.Length != _pnames.Length || types.Length != _ptypes.Length) {
                return false;
            }
            if (!names.Zip(_pnames, AreNullOrEqual).All(b => b)) {
                return false;
            }
            if (!types.Zip(_ptypes, AreNullOrEqual).All(b => b)) {
                return false;
            }
            if (!defaults.Zip(_pdefaults, AreNullOrEqual).All(b => b)) {
                return false;
            }

            for (var i = 0; i < _pnames.Length; ++i) {
                _pnames[i] = ChooseBest(_pnames[i], names[i]);
                _ptypes[i] = ChooseBest(_ptypes[i], types[i]);
                _pdefaults[i] = ChooseBest(_pdefaults[i], defaults[i]);
            }

            if (string.IsNullOrEmpty(_name)) {
                _name = name;
            }
            if (string.IsNullOrEmpty(_doc)) {
                _doc = documentation;
            }

            if (returnTypes != null) {
                _rtypes.UnionWith(returnTypes);
            }

            return true;
        }

        public OverloadResult ToOverloadResult() {
            var parameters = new ParameterResult[_pnames.Length];
            for (var i = 0; i < parameters.Length; ++i) {
                if (string.IsNullOrEmpty(_pnames[i])) {
                    return null;
                }

                parameters[i] = new ParameterResult(
                    _pnames[i],
                    null,
                    _ptypes[i],
                    false,
                    null,
                    _pdefaults[i]
                );
            }
            return new OverloadResult(parameters, _name, _doc, _rtypes);
        }
    }

    class BuiltinFunctionOverloadResult : OverloadResult {
        private readonly IPythonFunctionOverload _overload;
        private ParameterResult[] _parameters;
        private readonly ParameterResult[] _extraParameters;
        private readonly int _removedParams;
        private readonly PythonAnalyzer _projectState;
        private readonly Func<string> _fallbackDoc;
        private string _doc;
        private IReadOnlyList<string> _returnTypes;

        // Used by ToString to ensure docs have completed
        private Task _docTask;

        internal BuiltinFunctionOverloadResult(PythonAnalyzer state, string name, IPythonFunctionOverload overload, int removedParams, Func<string> fallbackDoc, params ParameterResult[] extraParams)
            : base(null, name, null, null) {
            _fallbackDoc = fallbackDoc;
            _overload = overload;
            _extraParameters = extraParams;
            _removedParams = removedParams;
            _projectState = state;
            _returnTypes = GetInstanceDescriptions(state, overload.ReturnType).OrderBy(n => n).Distinct().ToArray();

            Calculate();
        }

        internal BuiltinFunctionOverloadResult(PythonAnalyzer state, string name, IPythonFunctionOverload overload, int removedParams, params ParameterResult[] extraParams)
            : this(state, name, overload, removedParams, null, extraParams) {
        }

        private static IEnumerable<string> GetInstanceDescriptions(PythonAnalyzer state, IEnumerable<IPythonType> type) {
            foreach (var t in type) {
                var av = state.GetAnalysisValueFromObjects(t);
                var inst = av?.GetInstanceType();
                if (inst.IsUnknown()) {
                    yield return t.Name;
                } else {
                    foreach (var d in inst.GetShortDescriptions()) {
                        yield return d;
                    }
                }
            }
        }

        internal override OverloadResult WithNewParameters(ParameterResult[] newParameters) {
            return new BuiltinFunctionOverloadResult(
                _projectState,
                Name,
                _overload,
                _overload.GetParameters()?.Length ?? 0,
                _fallbackDoc,
                newParameters
            );
        }

        internal override OverloadResult WithoutLeadingParameters(int skipCount = 1) {
            return new BuiltinFunctionOverloadResult(_projectState, Name, _overload, skipCount, _fallbackDoc);
        }

        public override string Documentation {
            get {
                if (_docTask != null) {
                    _docTask.Wait(200);
                }
                return _doc;
            }
        }

        private void Calculate() {
            // initially fill in w/ a string saying we don't yet have the documentation
            _doc = Resources.CalculatingDocumentation;
            _docTask = Task.Factory.StartNew(DocCalculator);
        }

        public override string ToString() {
            _docTask?.Wait();
            return base.ToString();
        }

        private void DocCalculator() {
            var doc = new StringBuilder();
            if (!string.IsNullOrEmpty(_overload.Documentation)) {
                doc.AppendLine(_overload.Documentation);
            }

            foreach (var param in _overload.GetParameters()) {
                if (!string.IsNullOrEmpty(param.Documentation)) {
                    doc.AppendLine();
                    doc.Append(param.Name);
                    doc.Append(": ");
                    doc.Append(param.Documentation);
                }
            }

            if (!string.IsNullOrEmpty(_overload.ReturnDocumentation)) {
                doc.AppendLine();
                doc.AppendLine();
                doc.Append("Returns: ");
                doc.Append(_overload.ReturnDocumentation);
            }

            if (doc.Length == 0 && _fallbackDoc != null) {
                _doc = _fallbackDoc();
            } else {
                _doc = doc.ToString();
            }
            _docTask = null;
        }

        public override ParameterResult[] Parameters {
            get {
                if (_parameters == null) {
                    if (_overload != null) {
                        var target = _overload;

                        var pinfo = _overload.GetParameters();
                        var result = new List<ParameterResult>(pinfo.Length + _extraParameters.Length);
                        var ignored = 0;
                        ParameterResult kwDict = null;
                        foreach (var param in pinfo) {
                            if (ignored < _removedParams) {
                                ignored++;
                            } else {
                                var paramResult = GetParameterResultFromParameterInfo(param);
                                if (param.IsKeywordDict) {
                                    kwDict = paramResult;
                                } else {
                                    result.Add(paramResult);
                                }
                            }
                        }

                        result.InsertRange(0, _extraParameters);

                        // always add kw dict last.  When defined in C# and combined w/ params 
                        // it has to come earlier than it's legally allowed in Python so we 
                        // move it to the end for intellisense purposes here.
                        if (kwDict != null) {
                            result.Add(kwDict);
                        }
                        _parameters = result.ToArray();
                    } else {
                        _parameters = new ParameterResult[0];
                    }
                }
                return _parameters;
            }
        }

        internal ParameterResult GetParameterResultFromParameterInfo(IParameterInfo param) {
            var name = param.Name;

            string typeName;
            if (param.ParameterTypes != null) {
                typeName = param.ParameterTypes.Where(p => p != _projectState.Types[BuiltinTypeId.NoneType]).Select(p => p.Name).FirstOrDefault();
            } else {
                typeName = "object";
            }
            if (param.IsParamArray) {
                name = "*" + name;
                if (param.ParameterTypes is IAdvancedPythonType advType && advType.IsArray) {
                    var elemType = advType.GetElementType();
                    if (elemType == _projectState.Types[BuiltinTypeId.Object]) {
                        typeName = "sequence";
                    } else {
                        typeName = elemType.Name + " sequence";
                    }
                }
            } else if (param.IsKeywordDict) {
                name = "**" + name;
                typeName = "object";
            }

            var isOptional = false;
            var defaultValue = param.DefaultValue;
            if (defaultValue != null && defaultValue.Length == 0) {
                isOptional = true;
                defaultValue = null;
            }

            return new ParameterResult(name, "", typeName, isOptional, null, defaultValue);
        }

        public override IReadOnlyList<string> ReturnType => _returnTypes;
    }

    class OverloadResultComparer : EqualityComparer<OverloadResult> {
        public static IEqualityComparer<OverloadResult> Instance = new OverloadResultComparer(false);
        public static IEqualityComparer<OverloadResult> WeakInstance = new OverloadResultComparer(true);

        private readonly bool _weak;

        private OverloadResultComparer(bool weak) {
            _weak = weak;
        }

        public override bool Equals(OverloadResult x, OverloadResult y) {
            if (x == null || y == null) {
                return x == null && y == null;
            }

            if (x.Name != y.Name || (!_weak && x.Documentation != y.Documentation)) {
                return false;
            }

            if (x.Parameters == null || y.Parameters == null) {
                return x.Parameters == null && y.Parameters == null;
            }

            if (x.Parameters.Length != y.Parameters.Length) {
                return false;
            }

            return _weak 
                ? x.Parameters.Select(p => p.Name).SequenceEqual(y.Parameters.Select(p => p.Name))
                : x.Parameters.SequenceEqual(y.Parameters);
        }

        public override int GetHashCode(OverloadResult obj) {
            // Don't use Documentation for hash code, since it changes over time
            // in some implementations of IOverloadResult.
            var hc = 552127 ^ obj.Name.GetHashCode();
            if (obj.Parameters != null) {
                foreach (var p in obj.Parameters) {
                    hc ^= _weak ? p.Name.GetHashCode() : p.GetHashCode();
                }
            }
            return hc;
        }
    }
}
