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
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed partial class ImportHandler {
        public bool HandleFromImport(FromImportStatement node) {
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
            HandleImportSearchResult(imports, null, null, node.Root, out var variableModule);
            AssignVariables(node, imports, variableModule);
            return false;
        }

        private void AssignVariables(FromImportStatement node, IImportSearchResult imports, PythonVariableModule variableModule) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (variableModule != null && names.Count == 1 && names[0].Name == "*") {
                // TODO: warn this is not a good style per
                // TODO: https://docs.python.org/3/faq/programming.html#what-are-the-best-practices-for-using-import-in-a-module
                // TODO: warn this is invalid if not in the global scope.
                HandleModuleImportStar(variableModule, imports, node.StartIndex, names[0]);
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                var memberName = names[i].Name;
                if (!string.IsNullOrEmpty(memberName)) {
                    var nameExpression = asNames[i] ?? names[i];
                    var variableName = nameExpression?.Name ?? memberName;
                    if (!string.IsNullOrEmpty(variableName)) {
                        DeclareVariable(variableModule, memberName, imports, variableName, node.StartIndex, nameExpression);
                    }
                }
            }
        }

        private void HandleModuleImportStar(PythonVariableModule variableModule, IImportSearchResult imports, int importPosition, NameExpression nameExpression) {
            if (variableModule.Module == Module) {
                // from self import * won't define any new members
                return;
            }
            // If __all__ is present, take it, otherwise declare all members from the module that do not begin with an underscore.
            var memberNames = imports is ImplicitPackageImport
                ? variableModule.GetMemberNames()
                : variableModule.Analysis.StarImportMemberNames ?? variableModule.GetMemberNames().Where(s => !s.StartsWithOrdinal("_"));

            foreach (var memberName in memberNames) {
                DeclareVariable(variableModule, memberName, imports, memberName, importPosition, nameExpression);
            }
        }

        /// <summary>
        /// Determines value of the variable and declares it. Value depends if source module has submodule
        /// that is named the same as the variable and/or it has internal variables named same as the submodule.
        /// </summary>
        /// <example>'from a.b import c' when 'c' is both submodule of 'b' and a variable declared inside 'b'.</example>
        /// <param name="variableModule">Source module of the variable such as 'a.b' in 'from a.b import c as d'. May be null if the module was not found.</param>
        /// <param name="memberName">Module member name such as 'c' in 'from a.b import c as d'.</param>
        /// <param name="imports">Import search result.</param>
        /// <param name="variableName">Name of the variable to declare, such as 'd' in 'from a.b import c as d'.</param>
        /// <param name="importPosition">Position of the import statement.</param>
        /// <param name="nameLocation">Location of the variable name expression.</param>
        private void DeclareVariable(PythonVariableModule variableModule, string memberName, IImportSearchResult imports, string variableName, int importPosition, Node nameLocation) {
            IMember value = Eval.UnknownType;

            if (variableModule != null) {
                // First try imports since child modules should win, i.e. in 'from a.b import c'
                // 'c' should be a submodule if 'b' has one, even if 'b' also declares 'c = 1'.
                value = GetValueFromImports(variableModule, imports as IImportChildrenSource, memberName);

                // First try exported or child submodules.
                value = value ?? variableModule.GetMember(memberName);

                // Value may be variable or submodule. If it is variable, we need it in order to add reference.
                var variable = variableModule.Analysis?.GlobalScope?.Variables[memberName];
                value = variable?.Value?.Equals(value) == true ? variable : value;

                // If nothing is exported, variables are still accessible.
                value = value ?? variableModule.Analysis?.GlobalScope?.Variables[memberName]?.Value ?? Eval.UnknownType;
            }
            
            // Do not allow imported variables to override local declarations
            var canOverwrite = CanOverwriteVariable(variableName, importPosition, value);
            
            // Do not declare references to '*'
            var locationExpression = nameLocation is NameExpression nex && nex.Name == "*" ? null : nameLocation;
            Eval.DeclareVariable(variableName, value, VariableSource.Import, locationExpression, canOverwrite);
            
            // Make sure module is loaded and analyzed.
            if (value is IPythonModule m) {
                ModuleResolution.GetOrLoadModule(m.Name);
            }
        }

        private bool CanOverwriteVariable(string name, int importPosition, IMember newValue) {
            var v = Eval.CurrentScope.Variables[name];
            if (v == null) {
                return true; // Variable does not exist
            }
            
            if(newValue.IsUnknown()) {
                return false; // Do not overwrite potentially good value with unknowns.
            }

            // Allow overwrite if import is below the variable. Consider
            //   x = 1
            //   x = 2
            //   from A import * # brings another x
            //   x = 3
            var references = v.References.Where(r => r.DocumentUri == Module.Uri).ToArray();
            if (references.Length == 0) {
                // No references to the variable in this file - the variable 
                // is imported from another module. OK to overwrite.
                return true;
            }

            var firstAssignmentPosition = references.Min(r => r.Span.ToIndexSpan(Ast).Start);
            return firstAssignmentPosition < importPosition;
        }

        private IMember GetValueFromImports(PythonVariableModule parentModule, IImportChildrenSource childrenSource, string memberName) {
            if (childrenSource == null || !childrenSource.TryGetChildImport(memberName, out var childImport)) {
                return null;
            }

            switch (childImport) {
                case ModuleImport moduleImport:
                    var module = ModuleResolution.GetOrLoadModule(moduleImport.FullName);
                    return module != null ? GetOrCreateVariableModule(module, parentModule, moduleImport.Name) : Interpreter.UnknownType;
                case ImplicitPackageImport packageImport:
                    return GetOrCreateVariableModule(packageImport.FullName, parentModule, memberName);
                default:
                    return null;
            }
        }

        private void SpecializeFuture(FromImportStatement node) {
            if (Interpreter.LanguageVersion.Is3x()) {
                return;
            }

            var printNameExpression = node.Names.FirstOrDefault(n => n?.Name == "print_function");
            if (printNameExpression != null) {
                var fn = new PythonFunctionType("print", new Location(Module), null, string.Empty);
                var o = new PythonFunctionOverload(fn, new Location(Module));
                var parameters = new List<ParameterInfo> {
                    new ParameterInfo("values", Interpreter.GetBuiltinType(BuiltinTypeId.Object), ParameterKind.List, null),
                    new ParameterInfo("sep", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.KeywordOnly, null),
                    new ParameterInfo("end", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.KeywordOnly, null),
                    new ParameterInfo("file", Interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.KeywordOnly, null)
                };
                o.SetParameters(parameters);
                o.SetReturnValue(Interpreter.GetBuiltinType(BuiltinTypeId.None), true);
                fn.AddOverload(o);
                Eval.DeclareVariable("print", fn, VariableSource.Import, printNameExpression);
            }
        }
    }
}
