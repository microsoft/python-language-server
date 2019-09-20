﻿// All rights reserved.
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
using System.IO;
using FluentAssertions;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Indexing;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class SymbolIndexWalkerTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        [TestMethod, Priority(0)]
        public void WalkerAssignments() {
            var code = @"x = 1
y = x
z = y";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
                new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(2, 1, 2, 2)),
                new HierarchicalSymbol("z", SymbolKind.Variable, new SourceSpan(3, 1, 3, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerAssignmentsParenthesized() {
            var code = @"(x) = 1";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 2, 1, 3)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerMultipleAssignments() {
            var code = @"x = y = z = 1";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
                new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 5, 1, 6)),
                new HierarchicalSymbol("z", SymbolKind.Variable, new SourceSpan(1, 9, 1, 10)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerUnderscore() {
            var code = @"_ = 1";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public void WalkerIfStatement() {
            var code = @"if foo():
    x = 1
elif bar():
    x = 2
else:
    y = 3
";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 5, 2, 6)),
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(4, 5, 4, 6)),
                new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(6, 5, 6, 6)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerTryExceptFinally() {
            var code = @"try:
    x = 1
except Exception:
    x = 2
finally:
    y = 3
";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 5, 2, 6)),
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(4, 5, 4, 6)),
                new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(6, 5, 6, 6)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerReassign() {
            var code = @"x = 1
x = 2";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerAugmentedAssign() {
            var code = @"x += 1";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerTopLevelConstant() {
            var code = @"FOO_BAR_3 = 1234";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("FOO_BAR_3", SymbolKind.Constant, new SourceSpan(1, 1, 1, 10)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerFunction() {
            var code = @"def func(x, y):
    z = x + y
    return z";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("func", SymbolKind.Function, new SourceSpan(1, 1, 3, 13), new SourceSpan(1, 5, 1, 9), new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 10, 1, 11)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 13, 1, 14)),
                    new HierarchicalSymbol("z", SymbolKind.Variable, new SourceSpan(2, 5, 2, 6)),
                }, FunctionKind.Function),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerFunctionStarredArgs() {
            var code = @"def func(*args, **kwargs): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("func", SymbolKind.Function, new SourceSpan(1, 1, 1, 31), new SourceSpan(1, 5, 1, 9), new[] {
                    new HierarchicalSymbol("args", SymbolKind.Variable, new SourceSpan(1, 11, 1, 15)),
                    new HierarchicalSymbol("kwargs", SymbolKind.Variable, new SourceSpan(1, 19, 1, 25)),
                }, FunctionKind.Function),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerFunctionUnderscoreArg() {
            var code = @"def func(_): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("func", SymbolKind.Function, new SourceSpan(1, 1, 1, 17), new SourceSpan(1, 5, 1, 9), new List<HierarchicalSymbol>(), FunctionKind.Function),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerImports() {
            var code = @"import sys
import numpy as np
from os.path import join as osjoin
from os.path import ( join as osjoin2, exists as osexists, expanduser )
";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("sys", SymbolKind.Module, new SourceSpan(1, 8, 1, 11)),
                new HierarchicalSymbol("np", SymbolKind.Module, new SourceSpan(2, 17, 2, 19)),
                new HierarchicalSymbol("osjoin", SymbolKind.Module, new SourceSpan(3, 29, 3, 35)),
                new HierarchicalSymbol("osjoin2", SymbolKind.Module, new SourceSpan(4, 31, 4, 38)),
                new HierarchicalSymbol("osexists", SymbolKind.Module, new SourceSpan(4, 50, 4, 58)),
                new HierarchicalSymbol("expanduser", SymbolKind.Module, new SourceSpan(4, 60, 4, 70)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerImportFromFuture() {
            var code = @"from __future__ import print_function";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public void WalkerClass() {
            var code = @"class Foo(object):
    ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 2, 8), new SourceSpan(1, 7, 1, 10), new List<HierarchicalSymbol>(), FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerClassConstant() {
            var code = @"class Foo(object):
    CONSTANT = 1234";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 2, 20), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("CONSTANT", SymbolKind.Constant, new SourceSpan(2, 5, 2, 13)),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerConstructor() {
            var code = @"class Foo(object):
    def __init__(self, x): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 2, 31), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("__init__", SymbolKind.Constructor, new SourceSpan(2, 5, 2, 31), new SourceSpan(2, 9, 2, 17), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(2, 18, 2, 22)),
                        new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 24, 2, 25)),
                    }, FunctionKind.Function),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerComplexAssignmentLeftHand() {
            var code = @"(x, [y, z]) = (1, [2, 3])";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 2, 1, 3)),
                new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 6, 1, 7)),
                new HierarchicalSymbol("z", SymbolKind.Variable, new SourceSpan(1, 9, 1, 10)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerMethod() {
            var code = @"class Foo(object):
    def foo(self, x): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 2, 26), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("foo", SymbolKind.Method, new SourceSpan(2, 5, 2, 26), new SourceSpan(2, 9, 2, 12), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(2, 13, 2, 17)),
                        new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 19, 2, 20)),
                    }, FunctionKind.Function),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerDoubleUnderscoreMethod() {
            var code = @"class Foo(object):
    def __lt__(self, x): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 2, 29), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("__lt__", SymbolKind.Operator, new SourceSpan(2, 5, 2, 29), new SourceSpan(2, 9, 2, 15), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(2, 16, 2, 20)),
                        new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 22, 2, 23)),
                    }, FunctionKind.Function),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerProperties() {
            var code = @"class Foo(object):
    @property
    def func1(self): ...

    @abstractproperty
    def func2(self): ...

    @classproperty
    def func3(self): ...

    @abstractclassproperty
    def func4(self): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 12, 25), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("func1", SymbolKind.Property, new SourceSpan(2, 5, 3, 25), new SourceSpan(3, 9, 3, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(3, 15, 3, 19)),
                    }, FunctionKind.Property),
                    new HierarchicalSymbol("func2", SymbolKind.Property, new SourceSpan(5, 5, 6, 25), new SourceSpan(6, 9, 6, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(6, 15, 6, 19)),
                    }, FunctionKind.Property),
                    new HierarchicalSymbol("func3", SymbolKind.Property, new SourceSpan(8, 5, 9, 25), new SourceSpan(9, 9, 9, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(9, 15, 9, 19)),
                    }, FunctionKind.Property),
                    new HierarchicalSymbol("func4", SymbolKind.Property, new SourceSpan(11, 5, 12, 25), new SourceSpan(12, 9, 12, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(12, 15, 12, 19)),
                    }, FunctionKind.Property),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerAbcProperties() {
            var code = @"class Foo(object):
    @abc.abstractproperty
    def func1(self): ...

    @abc.abstractclassproperty
    def func2(self): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 6, 25), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("func1", SymbolKind.Property, new SourceSpan(2, 5, 3, 25), new SourceSpan(3, 9, 3, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(3, 15, 3, 19)),
                    }, FunctionKind.Property),
                    new HierarchicalSymbol("func2", SymbolKind.Property, new SourceSpan(5, 5, 6, 25), new SourceSpan(6, 9, 6, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(6, 15, 6, 19)),
                    }, FunctionKind.Property),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerStaticMethods() {
            var code = @"class Foo(object):
    @staticmethod
    def func1(arg): ...

    @abstractstaticmethod
    def func2(arg): ...

    @abc.abstractstaticmethod
    def func3(arg): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 9, 24), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("func1", SymbolKind.Method, new SourceSpan(2, 5, 3, 24), new SourceSpan(3, 9, 3, 14), new[] {
                        new HierarchicalSymbol("arg", SymbolKind.Variable, new SourceSpan(3, 15, 3, 18)),
                    }, FunctionKind.StaticMethod),
                    new HierarchicalSymbol("func2", SymbolKind.Method, new SourceSpan(5, 5, 6, 24), new SourceSpan(6, 9, 6, 14), new[] {
                        new HierarchicalSymbol("arg", SymbolKind.Variable, new SourceSpan(6, 15, 6, 18)),
                    }, FunctionKind.StaticMethod),
                    new HierarchicalSymbol("func3", SymbolKind.Method, new SourceSpan(8, 5, 9, 24), new SourceSpan(9, 9, 9, 14), new[] {
                        new HierarchicalSymbol("arg", SymbolKind.Variable, new SourceSpan(9, 15, 9, 18)),
                    }, FunctionKind.StaticMethod),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerClassMethods() {
            var code = @"class Foo(object):
    @classmethod
    def func1(cls): ...

    @abstractclassmethod
    def func2(cls): ...

    @abc.abstractclassmethod
    def func3(cls): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 9, 24), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("func1", SymbolKind.Method, new SourceSpan(2, 5, 3, 24), new SourceSpan(3, 9, 3, 14), new[] {
                        new HierarchicalSymbol("cls", SymbolKind.Variable, new SourceSpan(3, 15, 3, 18)),
                    }, FunctionKind.ClassMethod),
                    new HierarchicalSymbol("func2", SymbolKind.Method, new SourceSpan(5, 5, 6, 24), new SourceSpan(6, 9, 6, 14), new[] {
                        new HierarchicalSymbol("cls", SymbolKind.Variable, new SourceSpan(6, 15, 6, 18)),
                    }, FunctionKind.ClassMethod),
                    new HierarchicalSymbol("func3", SymbolKind.Method, new SourceSpan(8, 5, 9, 24), new SourceSpan(9, 9, 9, 14), new[] {
                        new HierarchicalSymbol("cls", SymbolKind.Variable, new SourceSpan(9, 15, 9, 18)),
                    }, FunctionKind.ClassMethod),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerTopLevelFunctionDecorator() {
            var code = @"@something
def func1(x, y): ...

@something_else()
def func2(x, y): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("func1", SymbolKind.Function, new SourceSpan(1, 1, 2, 21), new SourceSpan(2, 5, 2, 10), new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 11, 2, 12)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(2, 14, 2, 15)),
                }, FunctionKind.Function),
                new HierarchicalSymbol("func2", SymbolKind.Function, new SourceSpan(4, 1, 5, 21), new SourceSpan(5, 5, 5, 10), new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(5, 11, 5, 12)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(5, 14, 5, 15)),
                }, FunctionKind.Function),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerClassFunctionDecorator() {
            var code = @"class Foo(object):
    @something
    def func1(self): ...
    
    @something_else()
    def func2(self): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 6, 25), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("func1", SymbolKind.Method, new SourceSpan(2, 5, 3, 25), new SourceSpan(3, 9, 3, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(3, 15, 3, 19)),
                    }, FunctionKind.Function),
                    new HierarchicalSymbol("func2", SymbolKind.Method, new SourceSpan(5, 5, 6, 25), new SourceSpan(6, 9, 6, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(6, 15, 6, 19)),
                    }, FunctionKind.Function),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerClassFunctionMultiDecorator() {
            var code = @"class Foo(object):
    @property
    @something
    def func1(self): ...

    @something
    @property
    def func2(self): ...";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 8, 25), new SourceSpan(1, 7, 1, 10), new[] {
                    new HierarchicalSymbol("func1", SymbolKind.Property, new SourceSpan(2, 5, 4, 25), new SourceSpan(4, 9, 4, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(4, 15, 4, 19)),
                    }, FunctionKind.Property),
                    new HierarchicalSymbol("func2", SymbolKind.Property, new SourceSpan(6, 5, 8, 25), new SourceSpan(8, 9, 8, 14), new[] {
                        new HierarchicalSymbol("self", SymbolKind.Variable, new SourceSpan(8, 15, 8, 19)),
                    }, FunctionKind.Property),
                }, FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerLambda() {
            var code = @"f = lambda x, y: x + y";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("<lambda>", SymbolKind.Function, new SourceSpan(1, 5, 1, 23), children: new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 12, 1, 13)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 15, 1, 16)),
                }, functionKind: FunctionKind.Function),
                new HierarchicalSymbol("f", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerForLoop() {
            var code = @"z = False
for [x, y, (p, q)] in [[1, 2, [3, 4]]]:
    z += x
else:
    z = None";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("z", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 6, 2, 7)),
                new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(2, 9, 2, 10)),
                new HierarchicalSymbol("p", SymbolKind.Variable, new SourceSpan(2, 13, 2, 14)),
                new HierarchicalSymbol("q", SymbolKind.Variable, new SourceSpan(2, 16, 2, 17)),
                new HierarchicalSymbol("z", SymbolKind.Variable, new SourceSpan(3, 5, 3, 6)),
                new HierarchicalSymbol("z", SymbolKind.Variable, new SourceSpan(5, 5, 5, 6)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerListComprehension() {
            var code = @"flat_list = [item for sublist in l for item in sublist]";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("<list comprehension>", SymbolKind.None, new SourceSpan(1, 13, 1, 56), children: new[] {
                    new HierarchicalSymbol("sublist", SymbolKind.Variable, new SourceSpan(1, 23, 1, 30)),
                    new HierarchicalSymbol("item", SymbolKind.Variable, new SourceSpan(1, 40, 1, 44)),
                }),
                new HierarchicalSymbol("flat_list", SymbolKind.Variable, new SourceSpan(1, 1, 1, 10)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerDictionaryComprehension() {
            var code = @"d = { x: y for x, y in zip(range(10), range(10)) }";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("<dict comprehension>", SymbolKind.None, new SourceSpan(1, 5, 1, 51), children: new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 16, 1, 17)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 19, 1, 20)),
                }),
                new HierarchicalSymbol("d", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerSetComprehension() {
            var code = @"s = { x for x in range(10) }";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("<set comprehension>", SymbolKind.None, new SourceSpan(1, 5, 1, 29), children: new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 13, 1, 14)),
                }),
                new HierarchicalSymbol("s", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerGenerator() {
            var code = @"g = (x + y for x, y in zip(range(10), range(10)))";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("<generator>", SymbolKind.None, new SourceSpan(1, 5, 1, 50), children: new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 16, 1, 17)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 19, 1, 20)),
                }),
                new HierarchicalSymbol("g", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerNestedListComprehension() {
            var code = @"l = [
    x for x in [
        y for y in range(10)
    ]
]";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("<list comprehension>", SymbolKind.None, new SourceSpan(1, 5, 5, 2), children: new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(2, 11, 2, 12)),
                    new HierarchicalSymbol("<list comprehension>", SymbolKind.None, new SourceSpan(2, 16, 4, 6), children: new[] {
                        new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(3, 15, 3, 16)),
                    }),
                }),
                new HierarchicalSymbol("l", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerIncompleteFunction() {
            var code = @"def func(x, y):";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("func", SymbolKind.Function, new SourceSpan(1, 1, 1, 16), new SourceSpan(1, 5, 1, 9), new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 10, 1, 11)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 13, 1, 14)),
                }, FunctionKind.Function),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerIncompleteClass() {
            var code = @"class Foo(object):";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("Foo", SymbolKind.Class, new SourceSpan(1, 1, 1, 19), new SourceSpan(1, 7, 1, 10), new List<HierarchicalSymbol>(), FunctionKind.Class),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerIncompleteAssign() {
            var code = @"x =";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerAugmentedAssignLambda() {
            var code = @"x += lambda x, y: x + y";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("<lambda>", SymbolKind.Function, new SourceSpan(1, 6, 1, 24), children: new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 13, 1, 14)),
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 16, 1, 17)),
                }, functionKind: FunctionKind.Function),
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerAnnotatedAssignments() {
            var code = @"x:int = 1";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2))
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerNamedExpression() {
            var code = @"a = 123
if b := a:
    print(b)
";

            var symbols = WalkSymbols(code, version: PythonLanguageVersion.V38);
            symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                new HierarchicalSymbol("a", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2)),
                new HierarchicalSymbol("b", SymbolKind.Variable, new SourceSpan(2, 4, 2, 5))
            });
        }

        [TestMethod, Priority(0)]
        public void WalkerNoNameFunction() {
            var code = @"def ():";

            var symbols = WalkSymbols(code);
            symbols.Should().BeEmpty();
        }

        private PythonAst GetParse(string code, PythonLanguageVersion version)
            => Parser.CreateParser(new StringReader(code), version).ParseFile();

        private IReadOnlyList<HierarchicalSymbol> WalkSymbols(string code, PythonLanguageVersion version = PythonLanguageVersion.V37) {
            var ast = GetParse(code, version);
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            return walker.Symbols;
        }
    }
}
