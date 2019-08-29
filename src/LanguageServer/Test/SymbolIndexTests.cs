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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Indexing;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class SymbolIndexTests {
        private const int maxSymbols = 1000;
        private readonly string _rootPath = "C:/root";

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
        public async Task IndexHierarchicalDocumentAsync() {
            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();
                index.Add(path, DocumentWithAst("x = 1"));

                var symbols = await index.HierarchicalDocumentSymbolsAsync(path);
                symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2))
                });
            }
        }

        private static ISymbolIndex MakeSymbolIndex() {
            return new SymbolIndex(Substitute.For<IFileSystem>(), PythonLanguageVersion.V38);
        }

        [TestMethod, Priority(0)]
        public async Task IndexHierarchicalDocumentUpdate() {
            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();

                index.Add(path, DocumentWithAst("x = 1"));

                var symbols = await index.HierarchicalDocumentSymbolsAsync(path);
                symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                    new HierarchicalSymbol("x", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2))
                });

                index.Add(path, DocumentWithAst("y = 1"));

                symbols = await index.HierarchicalDocumentSymbolsAsync(path);
                symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                    new HierarchicalSymbol("y", SymbolKind.Variable, new SourceSpan(1, 1, 1, 2))
                });
            }
        }

        [TestMethod, Priority(0)]
        public async Task IndexHierarchicalDocumentNotFoundAsync() {
            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();

                var symbols = await index.HierarchicalDocumentSymbolsAsync(path);
                symbols.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public async Task IndexWorkspaceSymbolsFlattenAsync() {
            const string code = @"class Foo(object):
    def foo(self, x): ...";

            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();
                index.Add(path, DocumentWithAst(code));

                var symbols = await index.WorkspaceSymbolsAsync("", maxSymbols);
                symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                    new FlatSymbol("Foo", SymbolKind.Class, path, new SourceSpan(1, 7, 1, 10)),
                    new FlatSymbol("foo", SymbolKind.Method, path, new SourceSpan(2, 9, 2, 12), "Foo"),
                    new FlatSymbol("self", SymbolKind.Variable, path, new SourceSpan(2, 13, 2, 17), "foo"),
                    new FlatSymbol("x", SymbolKind.Variable, path, new SourceSpan(2, 19, 2, 20), "foo")
                });
            }
        }

        [TestMethod, Priority(0)]
        public async Task IndexWorkspaceSymbolsFilteredAsync() {
            const string code = @"class Foo(object):
    def foo(self, x): ...";

            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();

                index.Add(path, DocumentWithAst(code));

                var symbols = await index.WorkspaceSymbolsAsync("x", maxSymbols);
                symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                    new FlatSymbol("x", SymbolKind.Variable, path, new SourceSpan(2, 19, 2, 20), "foo"),
                });
            }
        }

        [TestMethod, Priority(0)]
        public async Task IndexWorkspaceSymbolsNotFoundAsync() {
            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();
                var symbols = await index.WorkspaceSymbolsAsync("", maxSymbols);
                symbols.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public async Task IndexWorkspaceSymbolsCaseInsensitiveAsync() {
            const string code = @"class Foo(object):
    def foo(self, x): ...";

            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();
                index.Add(path, DocumentWithAst(code));

                var symbols = await index.WorkspaceSymbolsAsync("foo", maxSymbols);
                symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                    new FlatSymbol("Foo", SymbolKind.Class, path, new SourceSpan(1, 7, 1, 10)),
                    new FlatSymbol("foo", SymbolKind.Method, path, new SourceSpan(2, 9, 2, 12), "Foo"),
                });
            }
        }

        [TestMethod, Priority(0)]
        public void MarkAsPendingWaitsForUpdates() {
            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();

                index.Add(path, DocumentWithAst("x = 1"));
                index.MarkAsPending(path);
                using (var cts = new CancellationTokenSource()) {
                    var t = index.HierarchicalDocumentSymbolsAsync(path, cts.Token);
                    t.IsCompleted.Should().BeFalse();
                    cts.Cancel();
                    Func<Task> cancelled = async () => {
                        await t;
                    };
                    cancelled.Should().Throw<OperationCanceledException>();
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task SymbolsAfterPendingWaitsForUpdateAsync() {
            using (var index = MakeSymbolIndex()) {
                var path = TestData.GetDefaultModulePath();

                index.Add(path, DocumentWithAst("x = 1"));
                index.MarkAsPending(path);
                var t = index.WorkspaceSymbolsAsync("", maxSymbols);
                index.ReIndex(path, DocumentWithAst("x = 1"));
                var symbols = await t;
                symbols.Should().BeEquivalentToWithStrictOrdering(new[] {
                    new FlatSymbol("x", SymbolKind.Variable, path, new SourceSpan(1, 1, 1, 2))
                });
            }
        }

        private PythonAst GetParse(string code, PythonLanguageVersion version = PythonLanguageVersion.V37)
            => Parser.CreateParser(new StringReader(code), version).ParseFile();

        private IReadOnlyList<HierarchicalSymbol> WalkSymbols(string code, PythonLanguageVersion version = PythonLanguageVersion.V37) {
            var ast = GetParse(code);
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            return walker.Symbols;
        }

        private IDocument DocumentWithAst(string testCode, string filePath = null) {
            filePath = filePath ?? $"{_rootPath}/{testCode}.py";
            var doc = Substitute.For<IDocument>();
            doc.GetAstAsync().ReturnsForAnyArgs(Task.FromResult(MakeAst(testCode)));
            doc.Uri.Returns(new Uri(filePath));
            return doc;
        }

        private PythonAst MakeAst(string testCode) {
            var latestVersion = PythonVersions.LatestAvailable3X.Version.ToLanguageVersion();
            return Parser.CreateParser(MakeStream(testCode), latestVersion).ParseFile();
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }

    }
}
