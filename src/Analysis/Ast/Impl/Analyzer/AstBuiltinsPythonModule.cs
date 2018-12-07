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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AstBuiltinsPythonModule : AstScrapedPythonModule, IBuiltinPythonModule {
        // protected by lock(_members)
        private readonly ConcurrentBag<string> _hiddenNames = new ConcurrentBag<string>();

        public AstBuiltinsPythonModule(IPythonInterpreter interpreter)
            : base(BuiltinTypeId.Unknown.GetModuleName(interpreter.LanguageVersion), null, interpreter) {
        }

        public override IMember GetMember(string name) => _hiddenNames.Contains(name) ? null : base.GetMember(name);

        public IMember GetAnyMember(string name) => Members.TryGetValue(name, out var m) ? m : null;

        public override IEnumerable<string> GetMemberNames() => base.GetMemberNames().Except(_hiddenNames).ToArray();

        protected override Stream LoadCachedCode() {
            var path = Interpreter.InterpreterPath ?? "python.exe";
            return ModuleCache.ReadCachedModule(path);
        }

        protected override void SaveCachedCode(Stream code) {
            if (Interpreter.InterpreterPath != null) {
                ModuleCache.WriteCachedModule(Interpreter.InterpreterPath, code);
            }
        }

        protected override List<string> GetScrapeArguments(IPythonInterpreter interpreter)
            => !InstallPath.TryGetFile("scrape_module.py", out string sb)
                ? null
                : new List<string> { "-B", "-E", sb };

        protected override PythonWalker PrepareWalker(PythonAst ast) {
            var filePath = ModuleCache.GetCacheFilePath(Interpreter.InterpreterPath ?? "python.exe");
            var walker = new AstAnalysisWalker(Interpreter, ast, this, filePath, null, Members, warnAboutUndefinedValues: true, suppressBuiltinLookup: true) {
                CreateBuiltinTypes = true
            };
            return walker;
        }

        protected override void PostWalk(PythonWalker walker) {
            IPythonType boolType = null;
            IPythonType noneType = null;

            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                if (Members.TryGetValue("__{0}__".FormatInvariant(typeId), out var m) && m is AstPythonType biType && biType.IsBuiltin) {
                    if (typeId != BuiltinTypeId.Str && typeId != BuiltinTypeId.StrIterator) {
                        biType.TrySetTypeId(typeId);
                    }

                    if (biType.IsHidden) {
                        _hiddenNames.Add(biType.Name);
                    }
                    _hiddenNames.Add("__{0}__".FormatInvariant(typeId));

                    switch (typeId) {
                        case BuiltinTypeId.Bool:
                            boolType = boolType ?? biType;
                            break;
                        case BuiltinTypeId.NoneType:
                            noneType = noneType ?? biType;
                            break;
                    }
                }
            }
            _hiddenNames.Add("__builtin_module_names__");

            if (boolType != null) {
                Members["True"] = Members["False"] = new AstPythonConstant(boolType);
            }

            if (noneType != null) {
                Members["None"] = new AstPythonConstant(noneType);
            }

            base.PostWalk(walker);
        }
    }
}
