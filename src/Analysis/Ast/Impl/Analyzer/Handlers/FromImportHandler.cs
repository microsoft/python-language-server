﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed partial class ImportHandler {
        public bool HandleFromImport(FromImportStatement node, AssignmentAction assignmentAction = null) {
            if (Module.ModuleType == ModuleType.Specialized) {
                return false;
            }

            var rootNames = node.Root.Names;
            if (rootNames.Count == 1) {
                var rootName = rootNames[0].Name;
                if (rootName.EqualsOrdinal("__future__")) {
                    SpecializeFuture(node);
                    return false;
                }
            }

            var imports = ModuleResolution.CurrentPathResolver.FindImports(Module.FilePath, node);
            if (HandleImportSearchResult(imports, null, null, node.Root, out var variableModule)) {
                AssignVariables(node, imports, variableModule, assignmentAction);
            }
            return false;
        }

        private void AssignVariables(FromImportStatement node, IImportSearchResult imports, PythonVariableModule variableModule, AssignmentAction assignmentAction = null) {
            if (variableModule == null) {
                return;
            }

            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: warn this is not a good style per
                // TODO: https://docs.python.org/3/faq/programming.html#what-are-the-best-practices-for-using-import-in-a-module
                // TODO: warn this is invalid if not in the global scope.
                HandleModuleImportStar(variableModule, imports is ImplicitPackageImport, node, assignmentAction);
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                var memberName = names[i].Name;
                if (!string.IsNullOrEmpty(memberName)) {
                    var nameExpression = asNames[i] ?? names[i];
                    var variableName = nameExpression?.Name ?? memberName;
                    var exported = variableModule.Analysis?.GlobalScope.Variables[memberName] ?? variableModule.GetMember(memberName);
                    var value = exported ?? GetValueFromImports(variableModule, imports as IImportChildrenSource, memberName);
                    Eval.AssignVariable(variableName, value, VariableSource.Import, nameExpression, assignmentAction);
                }
            }
        }

        private void HandleModuleImportStar(PythonVariableModule variableModule, bool isImplicitPackage, Node expression, AssignmentAction assignmentAction = null) {
            if (variableModule.Module == Module) {
                // from self import * won't define any new members
                return;
            }

            // If __all__ is present, take it, otherwise declare all members from the module that do not begin with an underscore.
            var memberNames = isImplicitPackage 
                ? variableModule.GetMemberNames()
                : variableModule.Analysis.StarImportMemberNames ?? variableModule.GetMemberNames().Where(s => !s.StartsWithOrdinal("_"));

            foreach (var memberName in memberNames) {
                var member = variableModule.GetMember(memberName);
                if (member == null) {
                    Log?.Log(TraceEventType.Verbose, $"Undefined import: {variableModule.Name}, {memberName}");
                } else if (member.MemberType == PythonMemberType.Unknown) {
                    Log?.Log(TraceEventType.Verbose, $"Unknown import: {variableModule.Name}, {memberName}");
                }

                member = member ?? Eval.UnknownType;
                if (member is IPythonModule m) {
                    ModuleResolution.GetOrLoadModule(m.Name);
                }

                var variable = variableModule.Analysis?.GlobalScope?.Variables[memberName];
                // When assigning variable from * we do not provide location so code navigation
                // will not be going to * and rather go straight to the member definition.
                Eval.AssignVariable(memberName, variable ?? member, VariableSource.Import, null, assignmentAction);
            }
        }

        private IMember GetValueFromImports(PythonVariableModule parentModule, IImportChildrenSource childrenSource, string memberName) {
            if (childrenSource == null || !childrenSource.TryGetChildImport(memberName, out var childImport)) {
                return Interpreter.UnknownType;
            }

            switch (childImport) {
                case ModuleImport moduleImport:
                    var module = ModuleResolution.GetOrLoadModule(moduleImport.FullName);
                    return module != null ? GetOrCreateVariableModule(module, parentModule, moduleImport.Name) : Interpreter.UnknownType;
                case ImplicitPackageImport packageImport:
                    return GetOrCreateVariableModule(packageImport.FullName, parentModule, memberName);
                default:
                    return Interpreter.UnknownType;
            }
        }

        private void SpecializeFuture(FromImportStatement node) {
            if (Interpreter.LanguageVersion.Is3x()) {
                return;
            }

            var printNameExpression = node.Names.FirstOrDefault(n => n?.Name == "print_function");
            if (printNameExpression != null) {
                var fn = new PythonFunctionType("print", new Location(Module), null, string.Empty);
                var o = new PythonFunctionOverload(fn.Name, new Location(Module));
                var parameters = new List<ParameterInfo> {
                    new ParameterInfo("*values", Interpreter.GetBuiltinType(BuiltinTypeId.Object), ParameterKind.List, null),
                    new ParameterInfo("sep", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.KeywordOnly, null),
                    new ParameterInfo("end", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.KeywordOnly, null),
                    new ParameterInfo("file", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.KeywordOnly, null)
                };
                o.SetParameters(parameters);
                o.SetReturnValue(Interpreter.GetBuiltinType(BuiltinTypeId.NoneType), true);
                fn.AddOverload(o);
                Eval.DeclareVariable("print", fn, VariableSource.Import, printNameExpression);
            }
        }
    }
}
