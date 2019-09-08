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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Module.Name} : {Module.ModuleType}")]
    internal class ModuleWalker : AnalysisWalker {
        private const string AllVariableName = "__all__";
        private readonly IDocumentAnalysis _stubAnalysis;
        private readonly CancellationToken _cancellationToken;

        // A hack to use __all__ export in the most simple case.
        private int _allReferencesCount;
        private bool _allIsUsable = true;

        public ModuleWalker(IServiceContainer services, IPythonModule module, PythonAst ast, CancellationToken cancellationToken)
            : base(new ExpressionEval(services, module, ast)) {
            _stubAnalysis = Module.Stub is IDocument doc ? doc.GetAnyAnalysis() : null;
            _cancellationToken = CancellationToken.None;
        }

        public override bool Walk(NameExpression node) {
            if (Eval.CurrentScope == Eval.GlobalScope && node.Name == AllVariableName) {
                _allReferencesCount++;
            }

            _cancellationToken.ThrowIfCancellationRequested();
            return base.Walk(node);
        }

        public override bool Walk(AugmentedAssignStatement node) {
            HandleAugmentedAllAssign(node);
            _cancellationToken.ThrowIfCancellationRequested();
            return base.Walk(node);
        }

        public override bool Walk(CallExpression node) {
            HandleAllAppendExtend(node);
            _cancellationToken.ThrowIfCancellationRequested();
            return base.Walk(node);
        }

        private void HandleAugmentedAllAssign(AugmentedAssignStatement node) {
            if (!IsHandleableAll(node.Left)) {
                return;
            }

            if (node.Right is ErrorExpression) {
                return;
            }

            if (node.Operator != Parsing.PythonOperator.Add) {
                _allIsUsable = false;
                return;
            }

            var rightVar = Eval.GetValueFromExpression(node.Right);
            var right = rightVar as IPythonCollection;

            if (right == null) {
                _allIsUsable = false;
                return;
            }

            ExtendAll(node.Left, right);
        }

        private void HandleAllAppendExtend(CallExpression node) {
            if (!(node.Target is MemberExpression me)) {
                return;
            }

            if (!IsHandleableAll(me.Target)) {
                return;
            }

            if (node.Args.Count == 0) {
                return;
            }

            var arg = node.Args[0].Expression;
            var v = Eval.GetValueFromExpression(arg);
            if (v == null) {
                _allIsUsable = false;
                return;
            }

            IPythonCollection values = null;
            switch (me.Name) {
                case "append":
                    values = PythonCollectionType.CreateList(Module, new List<IMember> { v }, exact: true);
                    break;
                case "extend":
                    values = v as IPythonCollection;
                    break;
            }

            if (values == null) {
                _allIsUsable = false;
                return;
            }

            ExtendAll(me.Target, values);
        }

        private void ExtendAll(Node location, IPythonCollection values) {
            Eval.LookupNameInScopes(AllVariableName, out var scope, LookupOptions.Global);
            if (scope == null) {
                return;
            }

            var all = scope.Variables[AllVariableName]?.Value as IPythonCollection;
            var list = PythonCollectionType.CreateConcatenatedList(Module, all, values);
            var source = list.IsGeneric() ? VariableSource.Generic : VariableSource.Declaration;

            Eval.DeclareVariable(AllVariableName, list, source, location);
        }

        private bool IsHandleableAll(Node node) {
            // TODO: handle more complicated lvars
            if (!(node is NameExpression ne)) {
                return false;
            }

            return Eval.CurrentScope == Eval.GlobalScope && ne.Name == AllVariableName;
        }

        public override bool Walk(PythonAst node) {
            Check.InvalidOperation(() => Ast == node, "walking wrong AST");
            _cancellationToken.ThrowIfCancellationRequested();

            // Collect basic information about classes and functions in order
            // to correctly process forward references. Does not determine
            // types yet since at this time imports or generic definitions
            // have not been processed.
            SymbolTable.Build(Eval);

            // There are cases (see typeshed datetime stub) with constructs
            //   class A:
            //      def __init__(self, x: Optional[B]): ...
            //
            //   _A = A
            //
            //   class B:
            //      def func(self, x: Optional[_A])
            //
            // so evaluation of A -> B ends up incomplete since _A is not known yet.
            // Thus, when A type is created, we need to go and evaluate all assignments
            // that might be referring to it in the right hand side.
            if (Ast.Body is SuiteStatement ste) {
                foreach (var statement in ste.Statements.OfType<AssignmentStatement>()) {
                    if (statement.Left.Count == 1 && statement.Left[0] is NameExpression leftNex && statement.Right is NameExpression rightNex) {
                        var m = Eval.GetInScope<IPythonClassType>(rightNex.Name);
                        if (m != null) {
                            Eval.DeclareVariable(leftNex.Name, m, VariableSource.Declaration, leftNex);
                        }
                    }
                }
            }

            return base.Walk(node);
        }

        // Classes and functions are walked by their respective evaluators
        public override bool Walk(ClassDefinition node) {
            _cancellationToken.ThrowIfCancellationRequested();
            SymbolTable.Evaluate(node);
            return false;
        }

        public override bool Walk(FunctionDefinition node) {
            _cancellationToken.ThrowIfCancellationRequested();
            SymbolTable.Evaluate(node);
            return false;
        }

        public void Complete() {
            _cancellationToken.ThrowIfCancellationRequested();

            SymbolTable.EvaluateAll();
            SymbolTable.ReplacedByStubs.Clear();
            MergeStub();

            if (_allIsUsable && _allReferencesCount >= 1 && GlobalScope.Variables.TryGetVariable(AllVariableName, out var variable)
                && variable?.Value is IPythonCollection collection && collection.IsExact) {
                StarImportMemberNames = collection.Contents
                    .OfType<IPythonConstant>()
                    .Select(c => c.GetString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToImmutableArray();
            }

            Eval.ClearCache();
        }

        public GlobalScope GlobalScope => Eval.GlobalScope;
        public IReadOnlyList<string> StarImportMemberNames { get; private set; }

        /// <summary>
        /// Merges data from stub with the data from the module.
        /// </summary>
        /// <remarks>
        /// Types are taken from the stub while location and documentation comes from 
        /// source so code navigation takes user to the source and not to the stub. 
        /// Stub data, such as class methods are augmented by methods from source
        /// since stub is not guaranteed to be complete.
        /// </remarks>
        private void MergeStub() {
            _cancellationToken.ThrowIfCancellationRequested();

            if (Module.ModuleType == ModuleType.User || Module.ModuleType == ModuleType.Stub) {
                return;
            }
            // No stub, no merge.
            if (_stubAnalysis.IsEmpty()) {
                return;
            }
            // TODO: figure out why module is getting analyzed before stub.
            // https://github.com/microsoft/python-language-server/issues/907
            // Debug.Assert(!(_stubAnalysis is EmptyAnalysis));

            // Scraping process can pick up more functions than the stub contains
            // Or the stub can have definitions that scraping had missed. Therefore
            // merge is the combination of the two with the documentation coming
            // from the library source of from the scraped module.
            foreach (var v in _stubAnalysis.GlobalScope.Variables) {
                var stubType = v.Value.GetPythonType();
                if (stubType.IsUnknown()) {
                    continue;
                }
                // If stub says 'Any' but we have better type, keep the current type.
                if (stubType.DeclaringModule is TypingModule && stubType.Name == "Any") {
                    continue;
                }

                var sourceVar = Eval.GlobalScope.Variables[v.Name];
                var sourceType = sourceVar?.Value.GetPythonType();
                if (sourceType.IsUnknown()) {
                    continue;
                }

                if (sourceVar?.Source == VariableSource.Import &&
                   sourceVar.GetPythonType()?.DeclaringModule.Stub != null) {
                    // Keep imported types as they are defined in the library. For example,
                    // 'requests' imports NullHandler as 'from logging import NullHandler'.
                    // But 'requests' also declares NullHandler in its stub (but not in the main code)
                    // and that declaration does not have documentation or location. Therefore avoid
                    // taking types that are stub-only when similar type is imported from another
                    // module that also has a stub.
                    continue;
                }

                TryReplaceMember(v, sourceType, stubType);
            }

            UpdateVariables();
        }

        private void TryReplaceMember(IVariable v, IPythonType sourceType, IPythonType stubType) {
            // If type does not exist in module, but exists in stub, declare it unless it is an import.
            // If types are the classes, take class from the stub, then add missing members.
            // Otherwise, replace type by one from the stub.
            switch (sourceType) {
                case null:
                    // Nothing in source, but there is type in the stub. Declare it.
                    if (v.Source == VariableSource.Declaration || v.Source == VariableSource.Generic) {
                        Eval.DeclareVariable(v.Name, v.Value, v.Source);
                    }
                    break;

                case PythonClassType sourceClass:
                    // Transfer documentation first so we get class documentation
                    // that comes from the class definition win over one that may
                    // come from __init__ during the member merge below.
                    TransferDocumentationAndLocation(sourceClass, stubType);

                    // Replace the class entirely since stub members may use generic types
                    // and the class definition is important. We transfer missing members
                    // from the original class to the stub.
                    Eval.DeclareVariable(v.Name, v.Value, v.Source);

                    // Go through source class members and pick those that are
                    // not present in the stub class.
                    foreach (var name in sourceClass.GetMemberNames()) {
                        var sourceMember = sourceClass.GetMember(name);
                        if (sourceMember.IsUnknown()) {
                            continue; // Do not add unknowns to the stub.
                        }
                        var sourceMemberType = sourceMember?.GetPythonType();
                        if (sourceMemberType is IPythonClassMember cm && cm.DeclaringType != sourceClass) {
                            continue; // Only take members from this class and not from bases.
                        }
                        if (!IsAcceptableModule(sourceMemberType)) {
                            continue; // Member does not come from module or its submodules.
                        }

                            var stubMember = stubType.GetMember(name);
                        var stubMemberType = stubMember.GetPythonType();

                        // Get documentation from the current type, if any, since stubs
                        // typically do not contain documentation while scraped code does.
                        TransferDocumentationAndLocation(sourceMemberType, stubMemberType);

                        // If stub says 'Any' but we have better type, use member from the original class.
                        if (stubMember != null && !(stubType.DeclaringModule is TypingModule && stubType.Name == "Any")) {
                            continue; // Stub already have the member, don't replace.
                        }

                        (stubType as PythonType)?.AddMember(name, stubMember, overwrite: true);
                    }
                    break;

                case IPythonModule _:
                    // We do not re-declare modules.
                    break;

                default:
                    var stubModule = stubType.DeclaringModule;
                    if (stubType is IPythonModule || stubModule.ModuleType == ModuleType.Builtins) {
                        // Modules members that are modules should remain as they are, i.e. os.path
                        // should remain library with its own stub attached.
                        break;
                    }
                    // We do not re-declaring variables that are imported.
                    if (v.Source == VariableSource.Declaration) {
                        TransferDocumentationAndLocation(sourceType, stubType);
                        // Re-declare variable with the data from the stub.
                        var source = Eval.CurrentScope.Variables[v.Name]?.Source ?? v.Source;
                        Eval.DeclareVariable(v.Name, v.Value, source);
                    }

                    break;
            }
        }

        private void UpdateVariables() {
            // Second pass: if we replaced any classes by new from the stub, we need to update 
            // variables that may still be holding old content. For example, ctypes
            // declares 'c_voidp = c_void_p' so when we replace 'class c_void_p'
            // by class from the stub, we need to go and update 'c_voidp' variable.
            foreach (var v in GlobalScope.Variables) {
                var variableType = v.Value.GetPythonType();
                if (!variableType.DeclaringModule.Equals(Module) && !variableType.DeclaringModule.Equals(Module.Stub)) {
                    continue;
                }
                // Check if type that the variable references actually declared here.
                var typeInScope = GlobalScope.Variables[variableType.Name].GetPythonType();
                if (typeInScope == null || variableType == typeInScope) {
                    continue;
                }

                if (v.Value == variableType) {
                    Eval.DeclareVariable(v.Name, typeInScope, v.Source);
                } else if (v.Value is IPythonInstance) {
                    Eval.DeclareVariable(v.Name, new PythonInstance(typeInScope), v.Source);
                }
            }
        }

        private void TransferDocumentationAndLocation(IPythonType s, IPythonType d) {
            if (s.IsUnknown() || s.DeclaringModule.ModuleType == ModuleType.Builtins ||
                d.IsUnknown() || d.DeclaringModule.ModuleType == ModuleType.Builtins) {
                return; // Do not transfer location of unknowns or builtins
            }

            // Stub may be one for multiple modules - when module consists of several
            // submodules, there is typically only one stub for the main module.
            // Types from 'unittest.case' (library) are stubbed in 'unittest' stub.
            if (!IsAcceptableModule(s)) {
                return; // Do not change unrelated types.
            }

            // Documentation and location are always get transferred from module type
            // to the stub type and never the other way around. This makes sure that
            // we show documentation from the original module and goto definition
            // navigates to the module source and not to the stub.
            if (s != d && s is PythonType src && d is PythonType dst) {
                // If type is a class, then doc can either come from class definition node of from __init__.
                // If class has doc from the class definition, don't stomp on it.
                if (src is PythonClassType srcClass && dst is PythonClassType dstClass) {
                    // Higher lever source wins
                    if (srcClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Class ||
                       (srcClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Init && dstClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Base)) {
                        dstClass.SetDocumentation(srcClass.Documentation);
                    }
                } else {
                    // Sometimes destination (stub type) already has documentation. This happens when stub type 
                    // is used to augment more than one type. For example, in threading module RLock stub class
                    // replaces both  RLock function and _RLock class making 'factory' function RLock to look 
                    // like a class constructor. Effectively a single  stub type is used for more than one type
                    // in the source and two source types may have different documentation. Thus transferring doc
                    // from one source type affects documentation of another type. It may be better to clone stub
                    // type and separate instances for separate source type, but for now we'll just avoid stomping
                    // on the existing documentation. 
                    if (string.IsNullOrEmpty(dst.Documentation)) {
                        var srcDocumentation = src.Documentation;
                        if (!string.IsNullOrEmpty(srcDocumentation)) {
                            dst.SetDocumentation(srcDocumentation);
                        }
                    }
                }

                if (src.Location.IsValid) {
                    dst.Location = src.Location;
                }
            }
        }

        private bool IsAcceptableModule(IPythonType type) {
            var thisModule = Eval.Module;
            var typeModule = type.DeclaringModule;
            var typeMainModuleName = typeModule.Name.Split('.').FirstOrDefault();
            return typeModule.Equals(thisModule) || typeMainModuleName == thisModule.Name;
        }
    }
}
