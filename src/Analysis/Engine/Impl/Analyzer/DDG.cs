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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.DependencyResolution;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    internal class DDG : PythonWalker {
        internal AnalysisUnit _unit;
        internal ExpressionEvaluator _eval;
        private SuiteStatement _curSuite;
        public readonly HashSet<IPythonProjectEntry> AnalyzedEntries = new HashSet<IPythonProjectEntry>();
        private PathResolverSnapshot _pathResolver;
        private string _filePath;

        public void Analyze(Deque<AnalysisUnit> queue, CancellationToken cancel, Action<int> reportQueueSize = null, int reportQueueInterval = 1) {
            if (cancel.IsCancellationRequested) {
                return;
            }
            try {
                // Including a marker at the end of the queue allows us to see in
                // the log how frequently the queue empties.
                var endOfQueueMarker = new AnalysisUnit(null, null);
                int queueCountAtStart = queue.Count;
                int reportInterval = reportQueueInterval - 1;

                if (queueCountAtStart > 0) {
                    queue.Append(endOfQueueMarker);
                }

                while (queue.Count > 0 && !cancel.IsCancellationRequested) {
                    _unit = queue.PopLeft();

                    if (_unit == endOfQueueMarker) {
                        AnalysisLog.EndOfQueue(queueCountAtStart, queue.Count);
                        if (reportInterval < 0 && reportQueueSize != null) {
                            reportQueueSize(queue.Count);
                        }

                        queueCountAtStart = queue.Count;
                        if (queueCountAtStart > 0) {
                            queue.Append(endOfQueueMarker);
                        }
                        continue;
                    }

                    AnalysisLog.Dequeue(queue, _unit);
                    if (reportInterval == 0 && reportQueueSize != null) {
                        reportQueueSize(queue.Count);
                        reportInterval = reportQueueInterval - 1;
                    } else if (reportInterval > 0) {
                        reportInterval -= 1;
                    }

                    _unit.IsInQueue = false;
                    SetCurrentUnit(_unit);
                    AnalyzedEntries.Add(_unit.ProjectEntry);
                    _unit.Analyze(this, cancel);
                }

                if (reportQueueSize != null) {
                    reportQueueSize(0);
                }

                if (cancel.IsCancellationRequested) {
                    AnalysisLog.Cancelled(queue);
                }
            } finally {
                AnalysisLog.Flush();
                AnalyzedEntries.Remove(null);
            }
        }

        public void SetCurrentUnit(AnalysisUnit unit) {
            _eval = new ExpressionEvaluator(unit);
            _unit = unit;
            _pathResolver = ProjectState.CurrentPathResolver;
            _filePath = GlobalScope.ProjectEntry.FilePath;
        }

        public InterpreterScope Scope {
            get {
                return _eval.Scope;
            }
            set {
                _eval.Scope = value;
            }
        }

        public ModuleInfo GlobalScope => _unit.DeclaringModule;

        public PythonAnalyzer ProjectState => _unit.State;

        public override bool Walk(PythonAst node) {
            Debug.Assert(node == _unit.Ast);

            if (!ProjectState.Modules.TryImport(_unit.DeclaringModule.Name, out var existingRef)) {
                // publish our module ref now so that we don't collect dependencies as we'll be fully processed
                if (existingRef == null) {
                    ProjectState.Modules.SetModule(_unit.DeclaringModule.Name, _unit.DeclaringModule);
                } else {
                    existingRef.Module = _unit.DeclaringModule;
                }
            }

            return base.Walk(node);
        }

        /// <summary>
        /// Gets the function which we are processing code for currently or
        /// null if we are not inside of a function body.
        /// </summary>
        public FunctionScope CurrentFunction => CurrentContainer<FunctionScope>();

        public ClassScope CurrentClass => CurrentContainer<ClassScope>();

        private T CurrentContainer<T>() where T : InterpreterScope {
            return Scope.EnumerateTowardsGlobal.OfType<T>().FirstOrDefault();
        }

        public override bool Walk(AssignmentStatement node) {
            var valueType = _eval.Evaluate(node.Right);

            // For self assignments (e.g. "fob = fob"), include values from 
            // outer scopes, otherwise such assignments will always be unknown
            // because we use the unassigned variable for the RHS.
            var ne = node.Right as NameExpression;
            InterpreterScope oldScope;
            if (ne != null &&
                (oldScope = _eval.Scope).OuterScope != null &&
                (node.Left.OfType<NameExpression>().Any(n => n.Name == ne.Name) ||
                node.Left.OfType<ExpressionWithAnnotation>().Select(e => e.Expression).OfType<NameExpression>().Any(n => n.Name == ne.Name))) {
                try {
                    _eval.Scope = _eval.Scope.OuterScope;
                    valueType = valueType.Union(_eval.Evaluate(node.Right));
                } finally {
                    _eval.Scope = oldScope;
                }
            }

            foreach (var left in node.Left) {
                if (left is ExpressionWithAnnotation annoExpr && annoExpr.Annotation != null) {
                    var annoType = _eval.EvaluateAnnotation(annoExpr.Annotation);
                    if (annoType?.Any() == true) {
                        _eval.AssignTo(node, annoExpr.Expression, annoType);
                    }
                }

                _eval.AssignTo(node, left, valueType);
            }
            return false;
        }

        public override bool Walk(AugmentedAssignStatement node) {
            var right = _eval.Evaluate(node.Right);

            foreach (var x in _eval.Evaluate(node.Left)) {
                x.AugmentAssign(node, _unit, right);
            }
            return false;
        }

        public override bool Walk(GlobalStatement node) {
            foreach (var name in node.Names) {
                GlobalScope.Scope.GetVariable(name, _unit, name.Name);
            }
            return false;
        }

        public override bool Walk(NonlocalStatement node) {
            if (Scope.OuterScope != null) {
                foreach (var name in node.Names) {
                    Scope.OuterScope.GetVariable(name, _unit, name.Name);
                }
            }
            return false;
        }

        public override bool Walk(ClassDefinition node) {
            // Evaluate decorators for references
            // TODO: Should apply decorators when assigning the class
            foreach (var d in (node.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault()) {
                _eval.Evaluate(d);
            }

            return false;
        }

        public override bool Walk(ExpressionStatement node) {
            _eval.Evaluate(node.Expression);

            if (node.Expression is ExpressionWithAnnotation annoExpr && annoExpr.Annotation != null) {
                // The variable is technically unassigned. However, other engines do show completion
                // on annotated, but not assigned variables. See https://github.com/Microsoft/PTVS/issues/3608
                // Pylint does not flag 'name' as unassigned in
                //
                //  class Employee(NamedTuple):
                //      name: str
                //      id: int = 3
                //
                //  employee = Employee('Guido')
                //  print(employee.name)
                var annoType = _eval.EvaluateAnnotation(annoExpr.Annotation);
                if (annoType?.Any() == true) {
                    _eval.AssignTo(node, annoExpr.Expression, annoType);
                }
            }
            return false;
        }

        public override bool Walk(ForStatement node) {
            if (node.List != null) {
                var list = _eval.Evaluate(node.List);
                _eval.AssignTo(
                    node,
                    node.Left,
                    node.IsAsync ? list.GetAsyncEnumeratorTypes(node, _unit) : list.GetEnumeratorTypes(node, _unit)
                );
            }

            if (node.Body != null) {
                node.Body.Walk(this);
            }

            if (node.Else != null) {
                node.Else.Walk(this);
            }
            return false;
        }

        /// <summary>
        /// Creates the variable for containing an imported member.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="value"></param>
        /// <param name="addRef">True to make <paramref name="node"/> a reference to the imported member.</param>
        /// <param name="node">The imported location. If <paramref name="addRef"/> is true, this location will be marked as a reference of the imported value.</param>
        /// <param name="nameReference"></param>
        private void AssignImportedModuleOrMember(string variableName, IAnalysisSet value, bool addRef, Node node, NameExpression nameReference) {
            var v = Scope.CreateVariable(node, _unit, variableName, addRef);
            if (addRef && nameReference != null) {
                v.AddReference(nameReference, _unit);
            }

            v.IsAlwaysAssigned = true;

            if (!value.IsNullOrEmpty() && v.AddTypes(_unit, value)) {
                GlobalScope.ModuleDefinition.EnqueueDependents();
            }
        }

        private void SetVariableForImportedMember(IModule module, NameExpression importNameExpression, string importedName, NameExpression variableExpression, string variableName, bool addRef) {
            var importedMember = module.GetModuleMember(importNameExpression, _unit, importedName, addRef, Scope, variableName);
            if (variableExpression != null) {
                module.GetModuleMember(variableExpression, _unit, importedName, addRef, null, null);
            }

            if (importedMember.IsNullOrEmpty()) {
                importedMember = null;
            }

            AssignImportedModuleOrMember(variableName, importedMember, addRef, importNameExpression, variableExpression);
        }

        public override bool Walk(FromImportStatement node) {
            var importSearchResult = _pathResolver.FindImports(_filePath, node);

            switch (importSearchResult) {
                case ModuleImport moduleImports when TryGetModule(node.Root, moduleImports, out var module):
                case PossibleModuleImport possibleModuleImport when TryGetModule(node.Root, possibleModuleImport, out module):
                    ImportMembersFromModule(node, module);
                    return false;
                case PackageImport packageImports:
                    ImportModulesFromPackage(node, packageImports);
                    return false;
                case ImportNotFound notFound:
                    MakeUnresolvedImport(notFound.FullName, node.Root);
                    return false;
                case NoKnownParentPackage _:
                    MakeNoKnownParentPackageImport(node.Root);
                    return false;
                default:
                    return false;
            }
        }

        private void ImportMembersFromModule(FromImportStatement node, IModule module) {
            var names = node.Names;
            if (names.Count == 1 && names[0].Name == "*") {
                // import all module public members
                var publicMembers = module
                    .GetModuleMemberNames(GlobalScope.InterpreterContext)
                    .Where(n => !n.StartsWithOrdinal("_"));
                foreach (var member in publicMembers) {
                    // Don't add references to "*" node
                    SetVariableForImportedMember(module, names[0], member, null, member, false);
                }
                return;
            }

            var asNames = node.AsNames;

            var len = Math.Min(names.Count, asNames.Count);
            for (var i = 0; i < len; i++) {
                var importedName = names[i].Name;

                // incomplete import statement
                if (string.IsNullOrEmpty(importedName)) {
                    continue;
                }

                var variableName = asNames[i]?.Name ?? importedName;
                SetVariableForImportedMember(module, names[i], importedName, asNames[i], variableName, true);
            }
        }

        private void ImportModulesFromPackage(FromImportStatement node, PackageImport packageImport) {
            var names = node.Names;
            var asNames = node.AsNames;

            var importNames = names.Select(n => n.Name).ToArray();
            if (importNames.Length == 1 && importNames[0] == "*") {
                // TODO: Need tracking of previous imports to determine possible imports for namespace package
                MakeUnresolvedImport(packageImport.Name, node.Root);
                return;
            }

            foreach (var module in packageImport.Modules) {
                var index = importNames.IndexOf(module.Name, StringExtensions.EqualsOrdinal);
                if (index == -1) {
                    MakeUnresolvedImport(module.FullName, node.Root);
                    continue;
                }

                if (!ProjectState.Modules.TryImport(module.FullName, out var moduleReference)) {
                    MakeUnresolvedImport(module.FullName, node.Root);
                    continue;
                }

                _unit.DeclaringModule.AddModuleReference(module.FullName, moduleReference);
                moduleReference.Module.Imported(_unit);

                var importedModule = moduleReference.Module;
                var variableName = asNames[index]?.Name ?? importNames[index];
                AssignImportedModuleOrMember(variableName, importedModule, true, names[index], asNames[index]);
            }
        }

        internal static List<AnalysisValue> LookupBaseMethods(string name, IEnumerable<IAnalysisSet> mro, Node node, AnalysisUnit unit) {
            var result = new List<AnalysisValue>();
            foreach (var @class in mro.Skip(1)) {
                foreach (var curType in @class) {
                    bool isClass = curType is ClassInfo || curType is BuiltinClassInfo;
                    if (isClass) {
                        var value = curType.GetMember(node, unit, name);
                        if (value != null) {
                            result.AddRange(value);
                        }
                    }
                }
            }
            return result;
        }

        public override bool Walk(FunctionDefinition node) {
            InterpreterScope funcScope;
            if (_unit.InterpreterScope.TryGetNodeScope(node, out funcScope)) {
                var function = ((FunctionScope)funcScope).Function;
                var analysisUnit = (FunctionAnalysisUnit)function.AnalysisUnit;

                var curClass = Scope as ClassScope;
                if (curClass != null) {
                    var bases = LookupBaseMethods(
                        analysisUnit.Ast.Name,
                        curClass.Class.Mro,
                        analysisUnit.Ast,
                        analysisUnit
                    );
                    foreach (var method in bases.OfType<BuiltinMethodInfo>()) {
                        foreach (var overload in method.Function.Overloads) {
                            function.UpdateDefaultParameters(_unit, overload.GetParameters());
                        }
                    }

                    foreach (var @base in bases.OfType<FunctionInfo>()) {
                        @base.AddDerived(function);
                    }
                }
            }

            return false;
        }

        internal void WalkBody(Node node, AnalysisUnit unit) {
            var oldUnit = _unit;
            var eval = _eval;
            _unit = unit;
            _eval = new ExpressionEvaluator(unit);
            try {
                node.Walk(this);
            } finally {
                _unit = oldUnit;
                _eval = eval;
            }
        }

        public override bool Walk(IfStatement node) {
            foreach (var test in node.TestsInternal) {
                _eval.Evaluate(test.Test);

                var prevScope = Scope;

                TryPushIsInstanceScope(test, test.Test);

                test.Body.Walk(this);

                Scope = prevScope;
            }
            if (node.ElseStatement != null) {
                node.ElseStatement.Walk(this);
            }
            return false;
        }

        public override bool Walk(ImportStatement node) {
            var len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (var i = 0; i < len; i++) {
                var moduleImportExpression = node.Names[i];
                var asNameExpression = node.AsNames[i];

                var imports = _pathResolver.GetImportsFromAbsoluteName(_filePath, moduleImportExpression.Names.Select(n => n.Name), node.ForceAbsolute);
                switch (imports) {
                    case ModuleImport moduleImports when TryGetModule(moduleImportExpression, moduleImports, out var module):
                    case PossibleModuleImport possibleModuleImport when TryGetModule(moduleImportExpression, possibleModuleImport, out module):
                        ImportModule(node, module, moduleImportExpression, asNameExpression);
                        break;
                    case ImportNotFound notFound:
                        MakeUnresolvedImport(notFound.FullName, moduleImportExpression);
                        break;
                }
            }
            return true;
        }

        private void MakeUnresolvedImport(string name, Node spanNode) {
            _unit.DeclaringModule.AddUnresolvedModule(name);
            ProjectState.AddDiagnostic(spanNode, _unit, ErrorMessages.UnresolvedImport(name), DiagnosticSeverity.Warning, ErrorMessages.UnresolvedImportCode);
        }

        private void MakeNoKnownParentPackageImport(Node spanNode) {
            ProjectState.AddDiagnostic(spanNode, _unit, Resources.ErrorRelativeImportNoPackage, DiagnosticSeverity.Warning, ErrorMessages.UnresolvedImportCode);
        }

        private void ImportModule(in ImportStatement node, in IModule module, in DottedName moduleImportExpression, in NameExpression asNameExpression) {
            // "import fob.oar as baz" is handled as
            // baz = import_module('fob.oar')
            if (asNameExpression != null) {
                AssignImportedModuleOrMember(asNameExpression.Name, module, true, node.Names.LastOrDefault(), asNameExpression);
                return;
            }

            // "import fob.oar" is handled as
            // import_module('fob.oar')
            // fob = import_module('fob')
            var importNames = moduleImportExpression.Names;

            var existingDepth = 0;
            PythonPackage pythonPackage = null;
            if (Scope.TryGetVariable(importNames[0].Name, out var importVariable)) {
                var childPackage = importVariable.Types.OfType<PythonPackage>().FirstOrDefault();
                while (childPackage != null && existingDepth < importNames.Count - 1) {
                    existingDepth++;
                    pythonPackage = childPackage;
                    childPackage = pythonPackage.GetChildPackage(null, importNames[existingDepth].Name) as PythonPackage;
                }
            }

            var child = module;
            for (var i = importNames.Count - 2; i >= existingDepth; i--) {
                var childName = importNames[i + 1].Name;
                var parentName = importNames[i].Name;
                var parent = new PythonPackage(parentName, ProjectState);
                parent.AddChildModule(childName, child);
                child = parent;
            }

            if (pythonPackage == null) {
                AssignImportedModuleOrMember(importNames[0].Name, child, true, importNames[0], null);
            } else {
                pythonPackage.AddChildModule(importNames[existingDepth].Name, child);
            }
        }

        private bool TryGetModule(Node importNode, ModuleImport moduleImport, out IModule module) {
            var fullName = moduleImport.FullName;
            if (!ProjectState.Modules.TryImport(fullName, out var moduleReference)) {
                MakeUnresolvedImport(fullName, importNode);
                module = default;
                return false;
            }

            _unit.DeclaringModule.AddModuleReference(fullName, moduleReference);
            module = moduleReference.Module;
            module.Imported(_unit);
            return true;
        }

        private bool TryGetModule(Node importNode, PossibleModuleImport possibleModuleImport, out IModule module) {
            var fullName = possibleModuleImport.PrecedingModuleFullName;
            if (!ProjectState.Modules.TryImport(fullName, out var moduleReference)) {
                MakeUnresolvedImport(fullName, importNode);
                module = default;
                return false;
            }

            _unit.DeclaringModule.AddModuleReference(fullName, moduleReference);
            module = moduleReference.Module;
            module.Imported(_unit);

            var nameParts = possibleModuleImport.RemainingNameParts;
            for (var i = 0; i < nameParts.Count; i++) {
                var namePart = nameParts[i];
                var childModule = module.GetChildPackage(null, namePart);
                if (childModule == null) {
                    var unresolvedModuleName = string.Join(".", nameParts.Take(i + 1).Prepend(fullName));
                    MakeUnresolvedImport(unresolvedModuleName, importNode);
                    return false;
                }

                module = childModule;
                module.Imported(_unit);
            }

            return true;
        }

        public override bool Walk(ReturnStatement node) {
            var fnScope = CurrentFunction;
            if (fnScope == null || node.Expression == null) {
                return true;
            }

            var lookupRes = _eval.Evaluate(node.Expression);
            fnScope.AddReturnTypes(node, _unit, lookupRes);

            var function = fnScope.Function;
            var analysisUnit = (FunctionAnalysisUnit)function.AnalysisUnit;

            if (Scope.OuterScope is ClassScope curClass) {
                var bases = LookupBaseMethods(
                    analysisUnit.Ast.Name,
                    curClass.Class.Mro,
                    analysisUnit.Ast,
                    analysisUnit
                );

                foreach (FunctionInfo baseFunction in bases.OfType<FunctionInfo>()) {
                    var baseAnalysisUnit = (FunctionAnalysisUnit)baseFunction.AnalysisUnit;
                    baseAnalysisUnit.ReturnValue.AddTypes(_unit, lookupRes);
                }
            }
            return true;
        }

        public override bool Walk(WithStatement node) {
            foreach (var item in node.Items) {
                var ctxMgr = _eval.Evaluate(item.ContextManager);
                var enter = ctxMgr.GetMember(node, _unit, node.IsAsync ? "__aenter__" : "__enter__");
                var exit = ctxMgr.GetMember(node, _unit, node.IsAsync ? "__aexit__" : "__exit__");
                var ctxt = enter.Call(node, _unit, new[] { ctxMgr }, ExpressionEvaluator.EmptyNames).Resolve(_unit);
                var exitRes = exit.Call(node, _unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames).Resolve(_unit);
                if (node.IsAsync) {
                    ctxt = ctxt.Await(node, _unit);
                    exitRes.Await(node, _unit);
                }
                if (item.Variable != null) {
                    _eval.AssignTo(node, item.Variable, ctxt);
                }
            }

            return true;
        }

        public override bool Walk(PrintStatement node) {
            foreach (var expr in node.Expressions) {
                _eval.Evaluate(expr);
            }
            return false;
        }

        public override bool Walk(AssertStatement node) {
            TryPushIsInstanceScope(node, node.Test);

            _eval.EvaluateMaybeNull(node.Test);
            _eval.EvaluateMaybeNull(node.Message);
            return false;
        }

        private void TryPushIsInstanceScope(Node node, Expression test) {
            if (Scope.TryGetNodeScope(node, out var newScope)) {
                var isInstanceScope = (IsInstanceScope)newScope;

                // magic assert isinstance statement alters the type information for a node
                var namesAndExpressions = OverviewWalker.GetIsInstanceNamesAndExpressions(test);
                foreach (var nameAndExpr in namesAndExpressions) {
                    var name = nameAndExpr.Key;
                    var type = nameAndExpr.Value;

                    var typeObj = _eval.EvaluateMaybeNull(type);
                    isInstanceScope.CreateTypedVariable(name, _unit, name.Name, typeObj);
                }

                // push the scope, it will be popped when we leave the current SuiteStatement.
                Scope = newScope;
            }
        }

        public override bool Walk(SuiteStatement node) {
            var prevSuite = _curSuite;
            var prevScope = Scope;

            _curSuite = node;
            if (node.Statements != null) {
                foreach (var statement in node.Statements) {
                    statement.Walk(this);
                }
            }

            Scope = prevScope;
            _curSuite = prevSuite;
            return false;
        }

        public override bool Walk(DelStatement node) {
            foreach (var expr in node.Expressions) {
                DeleteExpression(expr);
            }
            return false;
        }

        private void DeleteExpression(Expression expr) {
            NameExpression name = expr as NameExpression;
            if (name != null) {
                Scope.CreateVariable(name, _unit, name.Name);
                return;
            }

            IndexExpression index = expr as IndexExpression;
            if (index != null) {
                var values = _eval.Evaluate(index.Target);
                var indexValues = _eval.Evaluate(index.Index);
                foreach (var value in values) {
                    value.DeleteIndex(index, _unit, indexValues);
                }
                return;
            }

            MemberExpression member = expr as MemberExpression;
            if (member != null) {
                if (!string.IsNullOrEmpty(member.Name)) {
                    var values = _eval.Evaluate(member.Target);
                    foreach (var value in values) {
                        value.DeleteMember(member, _unit, member.Name);
                    }
                }
                return;
            }

            ParenthesisExpression paren = expr as ParenthesisExpression;
            if (paren != null) {
                DeleteExpression(paren.Expression);
                return;
            }

            SequenceExpression seq = expr as SequenceExpression;
            if (seq != null) {
                foreach (var item in seq.Items) {
                    DeleteExpression(item);
                }
            }
        }

        public override bool Walk(RaiseStatement node) {
            _eval.EvaluateMaybeNull(node.Value);
            _eval.EvaluateMaybeNull(node.Traceback);
            _eval.EvaluateMaybeNull(node.ExceptType);
            _eval.EvaluateMaybeNull(node.Cause);
            return false;
        }

        public override bool Walk(WhileStatement node) {
            _eval.Evaluate(node.Test);

            node.Body.Walk(this);
            if (node.ElseStatement != null) {
                node.ElseStatement.Walk(this);
            }

            return false;
        }

        public override bool Walk(TryStatement node) {
            node.Body.Walk(this);
            if (node.Handlers != null) {
                foreach (var handler in node.Handlers) {
                    var test = AnalysisSet.Empty;
                    if (handler.Test != null) {
                        var testTypes = _eval.Evaluate(handler.Test);

                        if (handler.Target != null) {
                            foreach (var type in testTypes) {
                                ClassInfo klass = type as ClassInfo;
                                if (klass != null) {
                                    test = test.Union(klass.Instance.SelfSet);
                                }

                                BuiltinClassInfo builtinClass = type as BuiltinClassInfo;
                                if (builtinClass != null) {
                                    test = test.Union(builtinClass.Instance.SelfSet);
                                }
                            }

                            _eval.AssignTo(handler, handler.Target, test);
                        }
                    }

                    handler.Body.Walk(this);
                }
            }

            if (node.Finally != null) {
                node.Finally.Walk(this);
            }

            if (node.Else != null) {
                node.Else.Walk(this);
            }

            return false;
        }

        public override bool Walk(ExecStatement node) {
            if (node.Code != null) {
                _eval.Evaluate(node.Code);
            }
            if (node.Locals != null) {
                _eval.Evaluate(node.Locals);
            }
            if (node.Globals != null) {
                _eval.Evaluate(node.Globals);
            }
            return false;
        }
    }
}
