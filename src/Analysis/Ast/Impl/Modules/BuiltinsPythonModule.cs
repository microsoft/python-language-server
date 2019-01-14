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
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;

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
            : base(moduleName, ModuleType.Builtins, filePath, null, services, ModuleLoadOptions.None) { } // TODO: builtins stub

        public override IMember GetMember(string name) => _hiddenNames.Contains(name) ? null : base.GetMember(name);

        public IMember GetAnyMember(string name) => base.GetMember(name);

        public override IEnumerable<string> GetMemberNames() => base.GetMemberNames().Except(_hiddenNames).ToArray();

        protected override IEnumerable<string> GetScrapeArguments(IPythonInterpreter interpreter)
            => !InstallPath.TryGetFile("scrape_module.py", out var sb) ? null : new List<string> { "-B", "-E", sb };

        protected override void OnAnalysisComplete() {
            lock (AnalysisLock) {
                SpecializeTypes();
                SpecializeFunctions();
            }
        }

        private void SpecializeTypes() {
            IPythonType noneType = null;
            var isV3 = Interpreter.LanguageVersion.Is3x();

            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                var m = GetMember("__{0}__".FormatInvariant(typeId));
                if (!(m is PythonType biType && biType.IsBuiltin)) {
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
                    default:
                        biType.TrySetTypeId(typeId);
                        switch (typeId) {
                            case BuiltinTypeId.Bool:
                                _boolType = _boolType ?? biType;
                                break;
                            case BuiltinTypeId.NoneType:
                                noneType = noneType ?? biType;
                                break;
                        }
                        break;
                }
            }

            _hiddenNames.Add("__builtin_module_names__");

            if (_boolType != null) {
                Analysis.GlobalScope.DeclareVariable("True", _boolType, LocationInfo.Empty);
                Analysis.GlobalScope.DeclareVariable("False", _boolType, LocationInfo.Empty);
            }

            if (noneType != null) {
                Analysis.GlobalScope.DeclareVariable("None", noneType, LocationInfo.Empty);
            }

            foreach (var n in GetMemberNames()) {
                var t = GetMember(n).GetPythonType();
                if (t.TypeId == BuiltinTypeId.Unknown && t.MemberType != PythonMemberType.Unknown) {
                    (t as PythonType)?.TrySetTypeId(BuiltinTypeId.Type);
                }
            }
        }

        private void SpecializeFunctions() {
            // TODO: deal with commented out functions.
            SpecializeFunction("abs", BuiltinsSpecializations.Identity);
            SpecializeFunction("cmp", Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            SpecializeFunction("dir", BuiltinsSpecializations.ListOfStrings);
            SpecializeFunction("eval", Interpreter.GetBuiltinType(BuiltinTypeId.Object));
            SpecializeFunction("globals", BuiltinsSpecializations.DictStringToObject);
            SpecializeFunction(@"isinstance", _boolType);
            SpecializeFunction(@"issubclass", _boolType);
            SpecializeFunction(@"iter", BuiltinsSpecializations.Iterator);
            SpecializeFunction("locals", BuiltinsSpecializations.DictStringToObject);
            //SpecializeFunction(_builtinName, "max", ReturnUnionOfInputs);
            //SpecializeFunction(_builtinName, "min", ReturnUnionOfInputs);
            SpecializeFunction("next", BuiltinsSpecializations.Next);
            //SpecializeFunction(_builtinName, "open", SpecialOpen);
            SpecializeFunction("ord", Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            SpecializeFunction("pow", BuiltinsSpecializations.Identity);
            SpecializeFunction("range", BuiltinsSpecializations.Range);
            SpecializeFunction("type", BuiltinsSpecializations.TypeInfo);

            //SpecializeFunction(_builtinName, "range", RangeConstructor);
            //SpecializeFunction(_builtinName, "sorted", ReturnsListOfInputIterable);
            SpecializeFunction("sum", BuiltinsSpecializations.Identity);
            //SpecializeFunction(_builtinName, "super", SpecialSuper);
            SpecializeFunction("vars", BuiltinsSpecializations.DictStringToObject);
        }
    }
}
