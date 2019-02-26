using System;
using System.IO;
using System.Text;
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
            var parser = Parser.CreateParser(MakeStream("f'bla'"), PythonLanguageVersion.V36);
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
            var parser = Parser.CreateParser(MakeStream("f'bla'"), PythonLanguageVersion.V36);
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
            var parser = Parser.CreateParser(MakeStream("f'{{bla}}'"), PythonLanguageVersion.V36);
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

        public Stream MakeStream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));
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
    }
}
