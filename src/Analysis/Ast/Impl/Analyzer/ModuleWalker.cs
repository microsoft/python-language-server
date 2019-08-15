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
using System.Xml.Serialization;
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

        // A hack to use __all__ export in the most simple case.
        private int _allReferencesCount;
        private bool _allIsUsable = true;

        public ModuleWalker(IServiceContainer services, IPythonModule module, PythonAst ast)
            : base(new ExpressionEval(services, module, ast)) {
            _stubAnalysis = Module.Stub is IDocument doc ? doc.GetAnyAnalysis() : null;
        }

        public override bool Walk(NameExpression node) {
            if (Eval.CurrentScope == Eval.GlobalScope && node.Name == AllVariableName) {
                _allReferencesCount++;
            }
            return base.Walk(node);
        }

        public override bool Walk(AugmentedAssignStatement node) {
            HandleAugmentedAllAssign(node);
            return base.Walk(node);
        }

        public override bool Walk(CallExpression node) {
            HandleAllAppendExtend(node);
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
            SymbolTable.Evaluate(node);
            return false;
        }

        public override bool Walk(FunctionDefinition node) {
            SymbolTable.Evaluate(node);
            return false;
        }

        public void Complete() {
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
        /// Functions are taken from the stub by the function walker since
        /// information on the return type is needed during the analysis walk.
        /// However, if the module is compiled (scraped), it often lacks some
        /// of the definitions. Stub may contains those so we need to merge it in.
        /// </remarks>
        private void MergeStub() {
            if (Module.ModuleType == ModuleType.User) {
                return;
            }
            // No stub, no merge.
            if (_stubAnalysis == null) {
                return;
            }
            // TODO: figure out why module is getting analyzed before stub.
            // https://github.com/microsoft/python-language-server/issues/907
            // Debug.Assert(!(_stubAnalysis is EmptyAnalysis));

            // Note that scrape can pick up more functions than the stub contains
            // Or the stub can have definitions that scraping had missed. Therefore
            // merge is the combination of the two with the documentation coming
            // from the library source of from the scraped module.
            foreach (var v in _stubAnalysis.GlobalScope.Variables) {
                var stubType = v.Value.GetPythonType();
                if (stubType.IsUnknown()) {
                    continue;
                }

                var sourceVar = Eval.GlobalScope.Variables[v.Name];
                var sourceType = sourceVar?.Value.GetPythonType();

                // If stub says 'Any' but we have better type, keep the current type.
                if (stubType.IsUnknown() || (stubType.DeclaringModule is TypingModule && stubType.Name == "Any")) {
                    continue;
                }

                // If type does not exist in module, but exists in stub, declare it unless it is an import.
                // If types are the classes, take class from the stub, then add missing members.
                // Otherwise, replace type from one from the stub.
                switch (sourceType) {
                    case PythonClassType sourceClass when Module.Equals(sourceClass.DeclaringModule):
                        // Transfer documentation first so we get class documentation
                        // that came from class definition win over one that may
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
                            if(sourceMember.IsUnknown()) {
                                continue; // Anything is better than unknowns.
                            }
                            var sourceMemberType = sourceMember?.GetPythonType();

                            var stubMember = stubType.GetMember(name);
                            var stubMemberType = stubMember.GetPythonType();

                            // Don't augment types that do not come from this module.
                            if (sourceType.IsBuiltin || stubType.IsBuiltin) {
                                // If source type does not have an immediate member such as __init__() and
                                // rather have it inherited from object, we do not want to use the inherited
                                // since stub class may either have its own of inherits it from the object.
                                continue;
                            }

                            if (stubMemberType?.MemberType == PythonMemberType.Method && stubMemberType?.DeclaringModule.ModuleType == ModuleType.Builtins) {
                                // Leave methods coming from object at the object and don't copy them into the derived class.
                            }

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
                            // Re-declare variable with the data from the stub.
                            TransferDocumentationAndLocation(sourceType, stubType);
                            // TODO: choose best type between the scrape and the stub. Stub probably should always win.
                            var source = Eval.CurrentScope.Variables[v.Name]?.Source ?? v.Source;
                            Eval.DeclareVariable(v.Name, v.Value, source);
                        }

                        break;
                }
            }
        }

        private static void TransferDocumentationAndLocation(IPythonType s, IPythonType d) {
            if (s.IsUnknown() || d.IsBuiltin || s.IsBuiltin) {
                return; // Do not transfer location of unknowns or builtins
            }
            // Documentation and location are always get transferred from module type
            // to the stub type and never the other way around. This makes sure that
            // we show documentation from the original module and goto definition
            // navigates to the module source and not to the stub.
            if (s != d && s is PythonType src && d is PythonType dst) {
                // If type is a class, then doc can either come from class definition node of from __init__.
                // If class has doc from the class definition, don't stomp on it.
                var transferDoc = true;
                if (src is PythonClassType srcClass && dst is PythonClassType dstClass) {
                    // Higher lever source wins
                    if(srcClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Class ||
                       (srcClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Init && dstClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Base)) {
                        dstClass.SetDocumentation(srcClass.Documentation);
                        transferDoc = false;
                    }
                }

                // Sometimes destination (stub type) already has documentation. This happens when stub type 
                // is used to augment more than one type. For example, in threading module RLock stub class
                // replaces both  RLock function and _RLock class making 'factory' function RLock to look 
                // like a class constructor. Effectively a single  stub type is used for more than one type
                // in the source and two source types may have different documentation. Thus transferring doc
                // from one source type affects documentation of another type. It may be better to clone stub
                // type and separate instances for separate source type, but for now we'll just avoid stomping
                // on the existing documentation. 
                if (transferDoc && string.IsNullOrEmpty(dst.Documentation)) {
                    var srcDocumentation = src.Documentation;
                    if (!string.IsNullOrEmpty(srcDocumentation)) {
                        dst.SetDocumentation(srcDocumentation);
                    }
                }

                if (src.Location.IsValid) {
                    dst.Location = src.Location;
                }
            }
        }
    }
}
