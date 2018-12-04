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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstBuiltinsPythonModule : AstScrapedPythonModule, IBuiltinPythonModule {
        // protected by lock(_members)
        private readonly HashSet<string> _hiddenNames;

        public AstBuiltinsPythonModule(PythonLanguageVersion version)
            : base(BuiltinTypeId.Unknown.GetModuleName(version), null) {
            _hiddenNames = new HashSet<string>();
        }

        public override IMember GetMember(IModuleContext context, string name) {
            lock (_members) {
                if (_hiddenNames.Contains(name)) {
                    return null;
                }
            }
            return base.GetMember(context, name);
        }

        public IMember GetAnyMember(string name) {
            lock (_members) {
                _members.TryGetValue(name, out var m);
                return m;
            }
        }

        public override IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            lock (_members) {
                return base.GetMemberNames(moduleContext).Except(_hiddenNames).ToArray();
            }
        }

        protected override Stream LoadCachedCode(AstPythonInterpreter interpreter) {
            var path = interpreter.InterpreterPath ?? "python.exe";
            return interpreter.ModuleCache.ReadCachedModule(path);
        }

        protected override void SaveCachedCode(AstPythonInterpreter interpreter, Stream code) {
            if (interpreter.InterpreterPath != null) {
                interpreter.ModuleCache.WriteCachedModule(interpreter.InterpreterPath, code);
            }
        }

        protected override List<string> GetScrapeArguments(AstPythonInterpreter interpreter) {
            if (!InstallPath.TryGetFile("scrape_module.py", out string sb)) {
                return null;
            }

            return new List<string> { "-B", "-E", sb };
        }

        protected override PythonWalker PrepareWalker(AstPythonInterpreter interpreter, PythonAst ast) {
            string filePath = null;
#if DEBUG
            filePath = interpreter.ModuleCache.GetCacheFilePath(interpreter.InterpreterPath ?? "python.exe");
            const bool includeLocations = true;
#else
            const bool includeLocations = false;
#endif

            var walker = new AstAnalysisWalker(
                interpreter, interpreter.CurrentPathResolver, ast, this, filePath, null, _members,
                includeLocations,
                warnAboutUndefinedValues: true,
                suppressBuiltinLookup: true
            ) {
                CreateBuiltinTypes = true
            };

            return walker;
        }

        protected override void PostWalk(PythonWalker walker) {
            IPythonType boolType = null;
            IPythonType noneType = null;

            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                if (_members.TryGetValue("__{0}__".FormatInvariant(typeId), out var m) && m is AstPythonType biType && biType.IsBuiltin) {
                    if (typeId != BuiltinTypeId.Str && typeId != BuiltinTypeId.StrIterator) {
                        biType.TrySetTypeId(typeId);
                    }

                    if (biType.IsHidden) {
                        _hiddenNames.Add(biType.Name);
                    }
                    _hiddenNames.Add("__{0}__".FormatInvariant(typeId));

                    if (typeId == BuiltinTypeId.Bool) {
                        boolType = boolType ?? biType;
                    }

                    if (typeId == BuiltinTypeId.NoneType) {
                        noneType = noneType ?? biType;
                    }
                }
            }
            _hiddenNames.Add("__builtin_module_names__");

            if (boolType != null) {
                _members["True"] = _members["False"] = new AstPythonConstant(boolType);
            }

            if (noneType != null) {
                _members["None"] = new AstPythonConstant(noneType);
            }

            base.PostWalk(walker);
        }

    }
}
