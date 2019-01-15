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

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Indexing {
    internal class SymbolIndexWalker : PythonWalker {
        private static readonly Regex DoubleUnderscore = new Regex(@"^__.*__$", RegexOptions.Compiled);
        private static readonly Regex ConstantLike = new Regex(@"^[\p{Lu}\p{N}_]+$", RegexOptions.Compiled);

        private readonly PythonAst _ast;
        private readonly SymbolStack _stack = new SymbolStack();

        public SymbolIndexWalker(PythonAst ast) {
            _ast = ast;
        }

        public IReadOnlyList<HierarchicalSymbol> Symbols => _stack.Root;

        public override bool Walk(ClassDefinition node) {
            _stack.Enter(SymbolKind.Class);
            node.Body?.Walk(this);
            var children = _stack.Exit();

            _stack.AddSymbol(new HierarchicalSymbol(
                node.Name,
                SymbolKind.Class,
                node.GetSpan(_ast),
                node.NameExpression.GetSpan(_ast),
                children,
                FunctionKind.Class
            ));

            return false;
        }

        public override bool Walk(FunctionDefinition node) {
            _stack.Enter(SymbolKind.Function);
            foreach (var p in node.Parameters) {
                AddVarSymbol(p.NameExpression);
            }
            node.Body?.Walk(this);
            var children = _stack.Exit();

            var span = node.GetSpan(_ast);

            var ds = new HierarchicalSymbol(
                node.Name,
                SymbolKind.Function,
                span,
                node.IsLambda ? span : node.NameExpression.GetSpan(_ast),
                children,
                FunctionKind.Function
            );

            if (_stack.Parent == SymbolKind.Class) {
                switch (ds.Name) {
                    case "__init__":
                        ds.Kind = SymbolKind.Constructor;
                        break;
                    case var name when DoubleUnderscore.IsMatch(name):
                        ds.Kind = SymbolKind.Operator;
                        break;
                    default:
                        ds.Kind = SymbolKind.Method;

                        if (node.Decorators != null) {
                            foreach (var dec in node.Decorators.Decorators) {
                                var maybeKind = DecoratorExpressionToKind(dec);
                                if (maybeKind.HasValue) {
                                    ds.Kind = maybeKind.Value.Item1;
                                    ds._functionKind = maybeKind.Value.Item2;
                                    break;
                                }
                            }
                        }

                        break;
                }
            }

            _stack.AddSymbol(ds);

            return false;
        }

        public override bool Walk(ImportStatement node) {
            foreach (var (name, asName) in node.Names.Zip(node.AsNames, (a, b) => (a, b))) {
                if (asName == null) {
                    var span = name.GetSpan(_ast);
                    _stack.AddSymbol(new HierarchicalSymbol(name.MakeString(), SymbolKind.Module, span));
                } else {
                    var span = asName.GetSpan(_ast);
                    _stack.AddSymbol(new HierarchicalSymbol(asName.Name, SymbolKind.Module, span));
                }
            }

            return false;
        }

        public override bool Walk(FromImportStatement node) {
            foreach (var name in node.Names.Zip(node.AsNames, (name, asName) => asName ?? name)) {
                var span = name.GetSpan(_ast);
                _stack.AddSymbol(new HierarchicalSymbol(name.Name, SymbolKind.Module, span));
            }

            return false;
        }

        public override bool Walk(AssignmentStatement node) {
            node.Right?.Walk(this);
            foreach (var ne in node.Left.OfType<NameExpression>()) {
                AddVarSymbol(ne);
            }
            return false;
        }

        public override bool Walk(AugmentedAssignStatement node) {
            node.Right?.Walk(this);
            AddVarSymbol(node.Left as NameExpression);
            return false;
        }

        public override bool Walk(IfStatement node) {
            WalkAndDeclareAll(node.Tests);
            WalkAndDeclare(node.ElseStatement);

            return false;
        }

        public override bool Walk(TryStatement node) {
            WalkAndDeclare(node.Body);
            WalkAndDeclareAll(node.Handlers);
            WalkAndDeclare(node.Else);
            WalkAndDeclare(node.Finally);

            return false;
        }

        private void AddVarSymbol(NameExpression node) {
            if (node == null) {
                return;
            }

            var kind = SymbolKind.Variable;

            switch (node.Name) {
                case "*":
                case "_":
                    return;
                case var s when (_stack.Parent == null || _stack.Parent == SymbolKind.Class) && ConstantLike.IsMatch(s):
                    kind = SymbolKind.Constant;
                    break;
            }

            var span = node.GetSpan(_ast);

            _stack.AddSymbol(new HierarchicalSymbol(node.Name, kind, span));
        }

        private (SymbolKind, string)? DecoratorExpressionToKind(Expression exp) {
            switch (exp) {
                case NameExpression ne when NameIsProperty(ne.Name):
                case MemberExpression me when NameIsProperty(me.Name):
                    return (SymbolKind.Property, FunctionKind.Property);

                case NameExpression ne when NameIsStaticMethod(ne.Name):
                case MemberExpression me when NameIsStaticMethod(me.Name):
                    return (SymbolKind.Method, FunctionKind.StaticMethod);

                case NameExpression ne when NameIsClassMethod(ne.Name):
                case MemberExpression me when NameIsClassMethod(me.Name):
                    return (SymbolKind.Method, FunctionKind.ClassMethod);
            }

            return null;
        }

        private bool NameIsProperty(string name) =>
            name == "property"
            || name == "abstractproperty"
            || name == "classproperty"
            || name == "abstractclassproperty";

        private bool NameIsStaticMethod(string name) =>
            name == "staticmethod"
            || name == "abstractstaticmethod";

        private bool NameIsClassMethod(string name) =>
            name == "classmethod"
            || name == "abstractclassmethod";

        private void WalkAndDeclare(Node node) {
            if (node == null) {
                return;
            }

            _stack.EnterDeclared();
            node.Walk(this);
            _stack.ExitDeclaredAndMerge();
        }

        private void WalkAndDeclareAll(IEnumerable<Node> nodes) {
            foreach (var node in nodes.MaybeEnumerate()) {
                WalkAndDeclare(node);
            }
        }

        private class SymbolStack {
            private readonly Stack<(SymbolKind?, List<HierarchicalSymbol>)> _symbols;
            private readonly Stack<HashSet<string>> _declared = new Stack<HashSet<string>>(new[] { new HashSet<string>() });

            public List<HierarchicalSymbol> Root { get; } = new List<HierarchicalSymbol>();

            public SymbolKind? Parent => _symbols.Peek().Item1;

            public SymbolStack() {
                _symbols = new Stack<(SymbolKind?, List<HierarchicalSymbol>)>(new (SymbolKind?, List<HierarchicalSymbol>)[] { (null, Root) });
            }

            public void Enter(SymbolKind parent) {
                _symbols.Push((parent, new List<HierarchicalSymbol>()));
                EnterDeclared();
            }

            public List<HierarchicalSymbol> Exit() {
                ExitDeclared();
                return _symbols.Pop().Item2;
            }
            public void EnterDeclared() => _declared.Push(new HashSet<string>());

            public void ExitDeclared() => _declared.Pop();

            public void ExitDeclaredAndMerge() => _declared.Peek().UnionWith(_declared.Pop());

            public void AddSymbol(HierarchicalSymbol sym) {
                if (sym.Kind == SymbolKind.Variable && _declared.Peek().Contains(sym.Name)) {
                    return;
                }

                _symbols.Peek().Item2.Add(sym);
                _declared.Peek().Add(sym.Name);
            }
        }
    }
}
