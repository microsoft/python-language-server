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
using System.Text.RegularExpressions;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Indexing {
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
                                    ds.Kind = maybeKind.Value.kind;
                                    ds._functionKind = maybeKind.Value.functionKind;
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
            foreach (var (nameNode, nameString) in node.Names.Zip(node.AsNames, (name, asName) => asName != null ? (asName, asName.Name) : ((Node)name, name.MakeString()))) {
                var span = nameNode.GetSpan(_ast);
                _stack.AddSymbol(new HierarchicalSymbol(nameString, SymbolKind.Module, span));
            }

            return false;
        }

        public override bool Walk(FromImportStatement node) {
            if (node.IsFromFuture) {
                return false;
            }

            foreach (var name in node.Names.Zip(node.AsNames, (name, asName) => asName ?? name)) {
                var span = name.GetSpan(_ast);
                _stack.AddSymbol(new HierarchicalSymbol(name.Name, SymbolKind.Module, span));
            }

            return false;
        }

        public override bool Walk(AssignmentStatement node) {
            node.Right?.Walk(this);
            foreach (var exp in node.Left) {
                AddVarSymbolRecursive(exp);
            }

            return false;
        }

        public override bool Walk(NamedExpression namedExpr) {
            namedExpr.Value?.Walk(this);
            AddVarSymbolRecursive(namedExpr.Target);
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

        public override bool Walk(ForStatement node) {
            _stack.EnterDeclared();
            AddVarSymbolRecursive(node.Left);
            node.List?.Walk(this);
            node.Body?.Walk(this);
            _stack.ExitDeclaredAndMerge();

            _stack.EnterDeclared();
            node.Else?.Walk(this);
            _stack.ExitDeclaredAndMerge();

            return false;
        }

        public override bool Walk(ComprehensionFor node) {
            AddVarSymbolRecursive(node.Left);
            return base.Walk(node);
        }

        public override bool Walk(ListComprehension node) {
            _stack.Enter(SymbolKind.None);
            return base.Walk(node);
        }

        public override void PostWalk(ListComprehension node) => ExitComprehension(node);

        public override bool Walk(DictionaryComprehension node) {
            _stack.Enter(SymbolKind.None);
            return base.Walk(node);
        }

        public override void PostWalk(DictionaryComprehension node) => ExitComprehension(node);

        public override bool Walk(SetComprehension node) {
            _stack.Enter(SymbolKind.None);
            return base.Walk(node);
        }

        public override void PostWalk(SetComprehension node) => ExitComprehension(node);

        public override bool Walk(GeneratorExpression node) {
            _stack.Enter(SymbolKind.None);
            return base.Walk(node);
        }

        public override void PostWalk(GeneratorExpression node) => ExitComprehension(node);

        private void ExitComprehension(Comprehension node) {
            var children = _stack.Exit();
            var span = node.GetSpan(_ast);

            _stack.AddSymbol(new HierarchicalSymbol(
                $"<{node.NodeName}>",
                SymbolKind.None,
                span,
                children: children
            ));
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

        private void AddVarSymbolRecursive(Expression node) {
            if (node == null) {
                return;
            }

            switch (node) {
                case NameExpression ne:
                    AddVarSymbol(ne);
                    return;
                case ExpressionWithAnnotation ewa:
                    AddVarSymbolRecursive(ewa.Expression);
                    return;
                case ParenthesisExpression parenExpr:
                    AddVarSymbolRecursive(parenExpr.Expression);
                    return;
                case SequenceExpression se:
                    foreach (var item in se.Items.MaybeEnumerate()) {
                        AddVarSymbolRecursive(item);
                    }
                    return;
            }
        }

        private (SymbolKind kind, string functionKind)? DecoratorExpressionToKind(Expression exp) {
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
            private readonly Stack<(SymbolKind? kind, List<HierarchicalSymbol> symbols)> _symbols;
            private readonly Stack<HashSet<string>> _declared = new Stack<HashSet<string>>(new[] { new HashSet<string>() });

            public List<HierarchicalSymbol> Root { get; } = new List<HierarchicalSymbol>();

            public SymbolKind? Parent => _symbols.Peek().kind;

            public SymbolStack() {
                _symbols = new Stack<(SymbolKind?, List<HierarchicalSymbol>)>(new (SymbolKind?, List<HierarchicalSymbol>)[] { (null, Root) });
            }

            public void Enter(SymbolKind parent) {
                _symbols.Push((parent, new List<HierarchicalSymbol>()));
                EnterDeclared();
            }

            public List<HierarchicalSymbol> Exit() {
                ExitDeclared();
                return _symbols.Pop().symbols;
            }
            public void EnterDeclared() => _declared.Push(new HashSet<string>());

            public void ExitDeclared() => _declared.Pop();

            public void ExitDeclaredAndMerge() => _declared.Peek().UnionWith(_declared.Pop());

            public void AddSymbol(HierarchicalSymbol sym) {
                if (sym.Kind == SymbolKind.Variable && _declared.Peek().Contains(sym.Name)) {
                    return;
                }

                _symbols.Peek().symbols.Add(sym);
                _declared.Peek().Add(sym.Name);
            }
        }
    }
}
