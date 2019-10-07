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
using Microsoft.Python.Analysis.Specializations;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents builtins module. Typically generated for a given Python interpreter
    /// by running special 'scraper' Python script that generates Python code via
    /// introspection of the compiled built-in language types.
    /// </summary>
    internal sealed class BuiltinsPythonModule : CompiledPythonModule, IBuiltinsPythonModule {
        private readonly HashSet<string> _hiddenNames = new HashSet<string>();
        private IPythonType _boolType;

        public BuiltinsPythonModule(string moduleName, string filePath, IServiceContainer services)
            : base(moduleName, ModuleType.Builtins, filePath, null, false, false, services) { } // TODO: builtins stub & persistence

        public override IMember GetMember(string name) => _hiddenNames.Contains(name) ? null : base.GetMember(name);

        public IMember GetAnyMember(string name) => base.GetMember(name);

        public override IEnumerable<string> GetMemberNames() => base.GetMemberNames().Except(_hiddenNames).ToArray();

        protected override string[] GetScrapeArguments(IPythonInterpreter interpreter)
            => !InstallPath.TryGetFile("scrape_module.py", out var sb) ? null : new[] { "-W", "ignore", "-B", "-E", sb };

        protected override void OnAnalysisComplete() {
            SpecializeTypes();
            SpecializeFunctions();
            foreach (var n in GetMemberNames()) {
                GetMember(n).GetPythonType<PythonType>()?.MakeReadOnly();
            }

            base.OnAnalysisComplete();
        }

        private void SpecializeTypes() {
            var isV3 = Interpreter.LanguageVersion.Is3x();

            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                var m = GetMember("__{0}__".FormatInvariant(typeId));
                if (!(m is PythonType biType && biType.IsBuiltin)) {
                    continue;
                }

                if (biType.IsUnknown()) {
                    // Under no circumstances we modify the unknown type.
                    continue;
                }

                if (biType.IsHidden) {
                    _hiddenNames.Add(biType.Name);
                }

                _hiddenNames.Add("__{0}__".FormatInvariant(typeId));

                // In V2 Unicode string is 'Unicode' and regular string is 'Str' or 'Bytes'.
                // In V3 Unicode and regular strings are 'Str' and ASCII/byte string is 'Bytes'.
                switch (typeId) {
                    case BuiltinTypeId.Bytes: {
                            var id = !isV3 ? BuiltinTypeId.Str : BuiltinTypeId.Bytes;
                            biType.TrySetTypeId(id);
                            biType.AddMember(@"__iter__", BuiltinsSpecializations.__iter__(Interpreter, id), true);
                            break;
                        }
                    case BuiltinTypeId.BytesIterator: {
                            biType.TrySetTypeId(!isV3 ? BuiltinTypeId.StrIterator : BuiltinTypeId.BytesIterator);
                            break;
                        }
                    case BuiltinTypeId.Unicode: {
                            var id = isV3 ? BuiltinTypeId.Str : BuiltinTypeId.Unicode;
                            biType.TrySetTypeId(id);
                            biType.AddMember(@"__iter__", BuiltinsSpecializations.__iter__(Interpreter, id), true);
                            break;
                        }
                    case BuiltinTypeId.UnicodeIterator: {
                            biType.TrySetTypeId(isV3 ? BuiltinTypeId.StrIterator : BuiltinTypeId.UnicodeIterator);
                            break;
                        }
                    case BuiltinTypeId.Str: {
                            biType.AddMember(@"__iter__", BuiltinsSpecializations.__iter__(Interpreter, typeId), true);
                        }
                        break;
                    case BuiltinTypeId.Unknown:
                        break;
                    default:
                        biType.TrySetTypeId(typeId);
                        switch (typeId) {
                            case BuiltinTypeId.Bool:
                                _boolType = _boolType ?? biType;
                                break;
                        }
                        break;
                }
            }

            _hiddenNames.Add("__builtin_module_names__");

            var location = new Location(this);
            if (_boolType != null) {
                Analysis.GlobalScope.DeclareVariable("True", _boolType, VariableSource.Builtin, location);
                Analysis.GlobalScope.DeclareVariable("False", _boolType, VariableSource.Builtin, location);
            }

            Analysis.GlobalScope.DeclareVariable("None", new PythonNone(this), VariableSource.Builtin, location);

            foreach (var n in GetMemberNames()) {
                var t = GetMember(n).GetPythonType();
                if (t.TypeId == BuiltinTypeId.Unknown && t.MemberType != PythonMemberType.Unknown) {
                    if (t is PythonType pt) {
                        pt.TrySetTypeId(BuiltinTypeId.Type);
                        // For Python 3+ make sure base is object
                    }
                }
            }
        }

        private void SpecializeFunctions() {
            // TODO: deal with commented out functions.
            Analysis.SpecializeFunction("abs", BuiltinsSpecializations.Identity);
            Analysis.SpecializeFunction("cmp", Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            Analysis.SpecializeFunction("dir", BuiltinsSpecializations.ListOfStrings);
            Analysis.SpecializeFunction("eval", Interpreter.GetBuiltinType(BuiltinTypeId.Object));
            Analysis.SpecializeFunction("getattr", BuiltinsSpecializations.GetAttr);
            Analysis.SpecializeFunction("globals", BuiltinsSpecializations.DictStringToObject);
            Analysis.SpecializeFunction(@"isinstance", _boolType);
            Analysis.SpecializeFunction(@"issubclass", _boolType);
            Analysis.SpecializeFunction(@"iter", BuiltinsSpecializations.Iterator);
            Analysis.SpecializeFunction("len", Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            Analysis.SpecializeFunction("locals", BuiltinsSpecializations.DictStringToObject);
            Analysis.SpecializeFunction("max", BuiltinsSpecializations.Identity);
            Analysis.SpecializeFunction("min", BuiltinsSpecializations.Identity);
            Analysis.SpecializeFunction("next", BuiltinsSpecializations.Next);

            Analysis.SpecializeFunction("open", BuiltinsSpecializations.Open, OpenConstructor(), new[] { "io" });

            Analysis.SpecializeFunction("ord", Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            Analysis.SpecializeFunction("pow", BuiltinsSpecializations.Identity);
            Analysis.SpecializeFunction("range", BuiltinsSpecializations.Range);
            Analysis.SpecializeFunction("sum", BuiltinsSpecializations.CollectionItem);
            Analysis.SpecializeFunction("type", BuiltinsSpecializations.TypeInfo);
            Analysis.SpecializeFunction("vars", BuiltinsSpecializations.DictStringToObject);

            //SpecializeFunction(_builtinName, "range", RangeConstructor);
            //SpecializeFunction(_builtinName, "sorted", ReturnsListOfInputIterable);
            //SpecializeFunction(_builtinName, "super", SpecialSuper);
        }

        private IReadOnlyList<ParameterInfo> OpenConstructor() {
            if (Interpreter.LanguageVersion.Is2x()) {
                return new[] {
                    new ParameterInfo("name", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, null),
                    new ParameterInfo("mode", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, new PythonConstant("r", Interpreter.GetBuiltinType(BuiltinTypeId.Str))),
                    new ParameterInfo("buffering", Interpreter.GetBuiltinType(BuiltinTypeId.Int), ParameterKind.Normal, new PythonConstant(-1, Interpreter.GetBuiltinType(BuiltinTypeId.Int))),
                };
            } else {
                return new[] {
                    new ParameterInfo("file", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, null),
                    new ParameterInfo("mode", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, new PythonConstant("r", Interpreter.GetBuiltinType(BuiltinTypeId.Str))),
                    new ParameterInfo("buffering", Interpreter.GetBuiltinType(BuiltinTypeId.Int), ParameterKind.Normal, new PythonConstant(-1, Interpreter.GetBuiltinType(BuiltinTypeId.Int))),
                    new ParameterInfo("encoding", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, new PythonConstant(null, Interpreter.GetBuiltinType(BuiltinTypeId.None))),
                    new ParameterInfo("errors", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, new PythonConstant(null, Interpreter.GetBuiltinType(BuiltinTypeId.None))),
                    new ParameterInfo("newline", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, new PythonConstant(null, Interpreter.GetBuiltinType(BuiltinTypeId.None))),
                    new ParameterInfo("closefd", Interpreter.GetBuiltinType(BuiltinTypeId.Bool), ParameterKind.Normal, new PythonConstant(true, Interpreter.GetBuiltinType(BuiltinTypeId.Bool))),
                    new ParameterInfo("opener", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, new PythonConstant(null, Interpreter.GetBuiltinType(BuiltinTypeId.Str)))
                };
            }
        }
    }
}
