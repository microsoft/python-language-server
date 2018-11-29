﻿// Python Tools for Visual Studio
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
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
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
            var fact = interpreter.Factory as AstPythonInterpreterFactory;
            if (fact?.Configuration.InterpreterPath == null) {
                return fact.ModuleCache.ReadCachedModule("python.exe");
            }
            return fact.ModuleCache.ReadCachedModule(fact.Configuration.InterpreterPath);
        }

        protected override void SaveCachedCode(AstPythonInterpreter interpreter, Stream code) {
            var fact = interpreter.Factory as AstPythonInterpreterFactory;
            if (fact?.Configuration.InterpreterPath == null) {
                return;
            }
            fact.ModuleCache.WriteCachedModule(fact.Configuration.InterpreterPath, code);
        }

        protected override List<string> GetScrapeArguments(IPythonInterpreterFactory factory) {
            if (!InstallPath.TryGetFile("scrape_module.py", out string sb)) {
                return null;
            }

            return new List<string> { "-B", "-E", sb };
        }

        protected override PythonWalker PrepareWalker(IPythonInterpreter interpreter, PythonAst ast) {
#if DEBUG
            var fact = (interpreter as AstPythonInterpreter)?.Factory as AstPythonInterpreterFactory;
            var filePath = fact?.ModuleCache.GetCacheFilePath(fact?.Configuration.InterpreterPath ?? "python.exe");
            const bool includeLocations = true;
#else
            string filePath = null;
            const bool includeLocations = false;
#endif

            var walker = new AstAnalysisWalker(
                interpreter, ast, this, filePath, null, _members, 
                includeLocations, 
                warnAboutUndefinedValues: true, 
                suppressBuiltinLookup: true
            );
            walker.CreateBuiltinTypes = true;
            return walker;
        }

        protected override void PostWalk(PythonWalker walker) {
            IPythonType boolType = null;

            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                if (_members.TryGetValue("__{0}__".FormatInvariant(typeId), out var m) && m is AstPythonType biType) {
                    if (typeId != BuiltinTypeId.Str && typeId != BuiltinTypeId.StrIterator) {
                        biType.TrySetTypeId(typeId);
                    }

                    if (biType.IsHidden) {
                        _hiddenNames.Add(biType.Name);
                    }
                    _hiddenNames.Add("__{0}__".FormatInvariant(typeId));

                    if (typeId == BuiltinTypeId.Bool) {
                        boolType = m as IPythonType;
                    }
                }
            }
            _hiddenNames.Add("__builtin_module_names__");

            if (boolType != null) {
                _members["True"] = _members["False"] = new AstPythonConstant(boolType);
            }

            base.PostWalk(walker);
        }

    }
}
