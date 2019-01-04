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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Indexing {
    internal class IndexerWalker : PythonWalker {
        private readonly PythonAst _ast;

        private readonly Stack<HierarchicalSymbol> _scope = new Stack<HierarchicalSymbol>();
        private HierarchicalSymbol Parent => _scope.Count > 0 ? _scope.Peek() : null;

        private bool _skipNext;
        private bool SkipNext {
            get {
                var v = _skipNext;
                _skipNext = false;
                return v;
            }
            set { _skipNext = value; }
        }

        public List<HierarchicalSymbol> Symbols = new List<HierarchicalSymbol>();

        private void AddHierarchicalSymbol(HierarchicalSymbol sym) {
            if (SkipNext) {
                return;
            }

            var parent = Parent;

            if (parent == null) {
                Symbols.Add(sym);
            } else {
                parent.Children = parent.Children ?? new List<HierarchicalSymbol>();
                parent.Children.Add(sym);
            }
        }

        public IndexerWalker(PythonAst ast) {
            _ast = ast;
        }

        public override bool Walk(NameExpression node) {
            var kind = SymbolKind.Variable;

            switch (node.Name) {
                case "*":
                case "_":
                    return base.Walk(node);
                case var s when (Parent == null || Parent.Kind == SymbolKind.Class) && Regex.IsMatch(s, @"^[\p{Lu}\p{N}_]+$"):
                    kind = SymbolKind.Constant;
                    break;
            }

            var span = node.GetSpan(_ast);

            AddHierarchicalSymbol(new HierarchicalSymbol {
                Name = node.Name,
                Kind = kind,
                Range = span,
                SelectionRange = span,
            });

            return base.Walk(node);
        }

        public override bool Walk(ClassDefinition node) {
            var ds = new HierarchicalSymbol {
                Name = node.Name,
                Kind = SymbolKind.Class,
                Range = node.GetSpan(_ast),
                SelectionRange = node.NameExpression.GetSpan(_ast),
            };
            AddHierarchicalSymbol(ds);
            _scope.Push(ds);
            SkipNext = true;
            return base.Walk(node);
        }

        public override void PostWalk(ClassDefinition node) {
            _scope.Pop();
            base.PostWalk(node);
        }

        public override bool Walk(FunctionDefinition node) {
            if (node.IsLambda) {
                return base.Walk(node);
            }

            var ds = new HierarchicalSymbol {
                Name = node.Name,
                Kind = SymbolKind.Function,
                Range = node.GetSpan(_ast),
                SelectionRange = node.NameExpression.GetSpan(_ast),
            };

            if (Parent?.Kind == SymbolKind.Class) {
                switch (ds.Name) {
                    case "__init__":
                        ds.Kind = SymbolKind.Constructor;
                        break;
                    case var name when Regex.IsMatch(name, @"^__.*__$"):
                        ds.Kind = SymbolKind.Operator;
                        break;
                    default:
                        ds.Kind = SymbolKind.Method;

                        if (node.Decorators != null) {
                            foreach (var dec in node.Decorators.Decorators) {
                                var maybeKind = DecoratorExpressionToKind(dec);
                                if (maybeKind.HasValue) {
                                    ds.Kind = maybeKind.Value;
                                    break;
                                }
                            }
                        }

                        break;
                }
            }

            AddHierarchicalSymbol(ds);
            _scope.Push(ds);
            SkipNext = true;
            return base.Walk(node);
        }

        public override void PostWalk(FunctionDefinition node) {
            if (node.IsLambda) {
                base.PostWalk(node);
                return;
            }

            _scope.Pop();
            base.PostWalk(node);
        }

        public override bool Walk(Parameter node) {
            var span = node.GetSpan(_ast);
            AddHierarchicalSymbol(new HierarchicalSymbol {
                Name = node.Name,
                Kind = SymbolKind.Variable,
                Range = span,
                SelectionRange = span,
            });

            return base.Walk(node);
        }

        public override bool Walk(ImportStatement node) {
            foreach (var imp in node.Names.Zip(node.AsNames, (name, asName) => (name, asName))) {
                var span = imp.name.GetSpan(_ast);
                AddHierarchicalSymbol(new HierarchicalSymbol {
                    Name = imp.name.MakeString(),
                    Kind = SymbolKind.Package,
                    Range = span,
                    SelectionRange = span,
                });

                if (imp.asName != null) {
                    span = imp.asName.GetSpan(_ast);
                    AddHierarchicalSymbol(new HierarchicalSymbol {
                        Name = imp.asName.Name,
                        Kind = SymbolKind.Package,
                        Range = span,
                        SelectionRange = span,
                    });
                }
            }

            return base.Walk(node);
        }

        public override bool Walk(FromImportStatement node) {
            var root = node.Root;
            var rootSpan = root.GetSpan(_ast);
            AddHierarchicalSymbol(new HierarchicalSymbol {
                Name = root.MakeString(),
                Kind = SymbolKind.Package,
                Range = rootSpan,
                SelectionRange = rootSpan,
            });

            foreach (var imp in node.Names.Zip(node.AsNames, (name, asName) => (name, asName))) {
                var span = imp.name.GetSpan(_ast);
                AddHierarchicalSymbol(new HierarchicalSymbol {
                    Name = imp.name.Name,
                    Kind = SymbolKind.Package,
                    Range = span,
                    SelectionRange = span,
                });

                if (imp.asName != null) {
                    span = imp.asName.GetSpan(_ast);
                    AddHierarchicalSymbol(new HierarchicalSymbol {
                        Name = imp.asName.Name,
                        Kind = SymbolKind.Package,
                        Range = span,
                        SelectionRange = span,
                    });
                }
            }

            return base.Walk(node);
        }

        public override bool Walk(DecoratorStatement node) {
            return base.Walk(node);
        }

        private SymbolKind? DecoratorExpressionToKind(Expression exp) {
            switch (exp) {
                case NameExpression ne when 
                    ne.Name == "property" || ne.Name == "abstractproperty"
                    || ne.Name == "classproperty" || ne.Name == "abstractclassproperty":
                    return SymbolKind.Property;

                case CallExpression ce when ce.Target is NameExpression ne:
                    return DecoratorExpressionToKind(ne);

                case CallExpression ce when ce.Target is MemberExpression me:
                    return DecoratorExpressionToKind(me.Target);
            }

            return null;
        }
    }
}
