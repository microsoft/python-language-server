using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Python.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Parsing.Tests {
    [TestClass]
    public class FStringTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public void ParsesFStringExpression() {
            var parser = Parser.CreateParser(MakeReader("f'bla'"), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            bool foundFStringExpression = false;
            ast.Walk(new MyPythonWalker(expr => {
                if (expr is FStringExpression) {
                    foundFStringExpression = true;
                }
            }));
            if (!foundFStringExpression) {
                throw new InternalTestFailureException("FStringExpression was never found");
            }
        }

        [TestMethod, Priority(0)]
        public void FStringInternalSubString() {
            var parser = Parser.CreateParser(MakeReader("f'bla'"), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case ConstantExpression constExpr:
                        if (!constExpr.Value.Equals("bla")) {
                            throw new InternalTestFailureException("Internal const expr didn't match substring");
                        }
                        break;
                }
            }));
        }

        [TestMethod, Priority(0)]
        public void CanEscapeBraces() {
            var parser = Parser.CreateParser(MakeReader("f'{{bla}}'"), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case ConstantExpression constExpr:
                        if (!constExpr.Value.Equals("{bla}")) {
                            throw new InternalTestFailureException("Internal const expr didn't match substring");
                        }
                        break;
                }
            }));
        }

        [TestMethod, Priority(0)]
        public void FStringWithExpressionInside() {
            var parser = Parser.CreateParser(MakeReader(@"
            s = f'{x}'
"), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case FStringExpression fStringExpr:
                        if (fStringExpr.GetChildNodes().First() is NameExpression xVarExpr) {
                            xVarExpr.Name.Equals("x").Should().BeTrue();
                            found = true;
                        }
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void FStringWithStringInside() {
            char doubleQuote = '"';
            var number = 111;
            var code = $@"
            s = f'f{doubleQuote}{number}{doubleQuote}'
            ";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case FStringExpression outerFstring:
                        if (outerFstring.GetChildNodes().First() is FStringExpression innerFString) {
                            if (outerFstring.GetChildNodes().First() is ConstantExpression constExpr) {
                                constExpr.Value.Should().BeEquivalentTo("f\"111\"");
                            }
                        }
                        break;
                }
            }));
        }

        [TestMethod, Priority(0)]
        public void ConfusingSubExpression() {
            var code = @"
            s = f'''ok now this {f'is'} confusing'''
            ";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case ConstantExpression constExpr:
                        if (constExpr.Value.Equals("is")) {
                            found = true;
                        }
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void DoubleNestedFString() {
            var code = "f\"first: {f'''second {f'third {thing}' } '''} \"";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            var found2 = false;

            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case ConstantExpression constExpr:
                        found2 |= constExpr.Value.Equals("third ");
                        break;
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("thing");
                        break;
                }
            }));
            found2.Should().BeTrue();
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void NestedFString() {
            var code = "print(f'''first: {f'second {value}'} ''')";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("value");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void NestedFStringWithDoubleQuotes() {
            var code = "f\"first: {f'{thing}'}\"";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;

            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("thing");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void AnotherNestedFString() {
            var code = "print(f\"result: {f'{value}'} \")";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("value");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void FormatterOptions() {
            var code = @"f'{casa!r:#06x}'";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("casa");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void FormatterOptionsWithSubstring() {
            var code = @"f'{casa!r:{width}}'";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("width");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void TrickyParsing() {
            var code = "f'Hello \'{tricky + \"example\"}\''";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("tricky");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void SpaceBetweenOpeningBraces() {
            var code = "f'space between opening braces: { {thing for thing in (1, 2, 3)}}'";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("thing");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void fRawString() {
            var code = "fr'{thing}'";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("thing");
                        break;
                }
            }));
            found.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void bracesInsideDoubleBraces () {
            var code = "f'{{blablabla {thing} }}'";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            var found = false;
            ast.Walk(new MyPythonWalker(expr => {
                switch (expr) {
                    case NameExpression nameExpr:
                        found |= nameExpr.Name.Equals("thing");
                        break;
                }
            }));
            found.Should().BeTrue();
        }


        [TestMethod, Priority(0)]
        [ExpectedException(typeof(Exception))]
        public void CloseWithSingleBrace() {
            var code = @"f'{{ mistake}'";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
        }

        [TestMethod, Priority(0)]
        public void NoEndOfEscapedBraces() {
            var code = @"f'{{ mistake'";
            var parser = Parser.CreateParser(MakeReader(code), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
        }

        public TextReader MakeReader(string s) => new StringReader(s);
    }

    internal class MyPythonWalker : PythonWalker {
        private readonly Action<Expression> _t;
        public MyPythonWalker(Action<Expression> t) {
            _t = t;
        }

        public override bool Walk(FStringExpression node) {
            _t(node);
            return true;
        }

        public override bool Walk(ConstantExpression node) {
            _t(node);
            return true;
        }

        public override bool Walk(NameExpression node) {
            _t(node);
            return true;
        }
    }
}
