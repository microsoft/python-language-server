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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    internal sealed class ClassEvaluator : MemberEvaluator {
        private readonly ClassDefinition _classDef;
        private PythonClassType _class;

        public ClassEvaluator(ExpressionEval eval, ClassDefinition classDef) : base(eval, classDef) {
            _classDef = classDef;
        }

        public override void Evaluate() {
            EvaluateClass();
            Result = _class;
        }

        public void EvaluateClass() {
            // Open class scope chain
            using (Eval.OpenScope(Module, _classDef, out var outerScope)) {
                var instance = Eval.GetInScope(_classDef.Name, outerScope);
                if (!(instance?.GetPythonType() is PythonClassType classInfo)) {
                    if (instance != null) {
                        // TODO: warning that variable is already declared of a different type.
                    }
                    return;
                }

                // Evaluate inner classes, if any
                EvaluateInnerClasses(_classDef);
                _class = classInfo;
                
                var bases = ProcessBases();
                _class.SetBases(bases);
                // Declare __class__ variable in the scope.
                Eval.DeclareVariable("__class__", _class, VariableSource.Declaration);
                ProcessClassBody();
            }
        }

        private void ProcessClassBody() {
            // Class is handled in a specific order rather than in the order of
            // the statement appearance. This is because we need all members
            // properly declared and added to the class type so when we process
            // methods, the class variables are all declared and constructors
            // are evaluated.

            // Process bases.
            foreach (var b in _class.Bases.Select(b => b.GetPythonType<IPythonClassType>()).ExcludeDefault()) {
                SymbolTable.Evaluate(b.ClassDefinition);
            }

            // Process imports
            foreach (var s in GetStatements<FromImportStatement>(_classDef)) {
                ImportHandler.HandleFromImport(s);
            }
            foreach (var s in GetStatements<ImportStatement>(_classDef)) {
                ImportHandler.HandleImport(s);
            }
            UpdateClassMembers();

            // Process assignments so we get class variables declared.
            // Note that annotated definitions and assignments can be intermixed
            // and must be processed in order. Consider
            //    class A:
            //      x: int
            //      x = 1
            foreach (var s in GetStatements<Statement>(_classDef)) {
                switch (s) {
                    case AssignmentStatement assignment:
                        AssignmentHandler.HandleAssignment(assignment);
                        break;
                    case ExpressionStatement e:
                        AssignmentHandler.HandleAnnotatedExpression(e.Expression as ExpressionWithAnnotation, null);
                        break;
                }
            }
            UpdateClassMembers();

            // Ensure constructors are processed so class members are initialized.
            EvaluateConstructors(_classDef);
            UpdateClassMembers();

            // Process remaining methods.
            SymbolTable.EvaluateScope(_classDef);
            UpdateClassMembers();
        }

        private IEnumerable<IPythonType> ProcessBases() {
            var bases = new List<IPythonType>();
            foreach (var a in _classDef.Bases.Where(a => string.IsNullOrEmpty(a.Name))) {
                if (IsValidBase(a)) {
                    TryAddBase(bases, a);
                } else {
                    ReportInvalidBase(a.ToCodeString(Eval.Ast, CodeFormattingOptions.Traditional));
                }
            }

            return bases;
        }

        private bool IsValidBase(Arg a) {
            var expr = a.Expression;
            var m = Eval.GetValueFromExpression(expr);

            // Allow any unknown members
            if (m.IsUnknown()) {
                return true;
            }

            // Allow any members from typing module
            // TODO handle typing module specialization better: https://github.com/microsoft/python-language-server/issues/1367
            if (m is ILocatedMember l && l.DeclaringModule is TypingModule) {
                return true;
            }

            switch (m.MemberType) {
                // Inheriting from these members is invalid
                case PythonMemberType.Method:
                case PythonMemberType.Function:
                case PythonMemberType.Property:
                case PythonMemberType.Instance:
                case PythonMemberType.Variable when m is IPythonConstant:
                    return false;
            }

            // Optimistically say anything that passes these checks is a valid base 
            return true;
        }

        private void TryAddBase(List<IPythonType> bases, Arg arg) {
            // We cheat slightly and treat base classes as annotations.
            var b = Eval.GetTypeFromAnnotation(arg.Expression);
            if (b != null) {
                var t = b.GetPythonType();
                bases.Add(t);
                t.AddReference(Eval.GetLocationOfName(arg.Expression));
            }
        }

        private void EvaluateConstructors(ClassDefinition cd) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var constructors = SymbolTable.Evaluators
                .Where(kvp => kvp.Key.Parent == cd && (kvp.Key.Name == "__init__" || kvp.Key.Name == "__new__"))
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var ctor in constructors) {
                SymbolTable.Evaluate(ctor);
            }
        }

        private void EvaluateInnerClasses(ClassDefinition cd) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var innerClasses = SymbolTable.Evaluators
                .Where(kvp => kvp.Key.Parent == cd && (kvp.Key is ClassDefinition))
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var c in innerClasses) {
                SymbolTable.Evaluate(c);
            }
        }

        private void UpdateClassMembers() {
            // Add members from this file
            var members = Eval.CurrentScope.Variables.Where(v => v.Source == VariableSource.Declaration || v.Source == VariableSource.Import);
            _class.AddMembers(members, false);
        }

        private void ReportInvalidBase(string argVal) {
            Eval.ReportDiagnostics(Eval.Module.Uri,
                new DiagnosticsEntry(
                Resources.InheritNonClass.FormatInvariant(argVal),
                _classDef.NameExpression.GetLocation(Eval)?.Span ?? default,
                Diagnostics.ErrorCodes.InheritNonClass,
                Severity.Error,
                DiagnosticSource.Analysis
            ));
        }

        // Classes and functions are walked by their respective evaluators
        public override bool Walk(ClassDefinition node) => false;
        public override bool Walk(FunctionDefinition node) => false;
    }
}
