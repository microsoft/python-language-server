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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class LanguageServerTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        public static InterpreterConfiguration DefaultV3 {
            get {
                var ver = PythonVersions.Python36_x64 ?? PythonVersions.Python36 ??
                          PythonVersions.Python35_x64 ?? PythonVersions.Python35;
                ver.AssertInstalled();
                return ver;
            }
        }

        public static InterpreterConfiguration DefaultV2 {
            get {
                var ver = PythonVersions.Python27_x64 ?? PythonVersions.Python27;
                ver.AssertInstalled();
                return ver;
            }
        }

        protected virtual InterpreterConfiguration Default => DefaultV3;
        protected virtual BuiltinTypeId BuiltinTypeId_Str => BuiltinTypeId.Unicode;

        public Task<Server> CreateServer() {
            return CreateServer((Uri)null, Default);
        }

        public Task<Server> CreateServer(string rootPath, InterpreterConfiguration configuration = null, Dictionary<Uri, PublishDiagnosticsEventArgs> diagnosticEvents = null) {
            return CreateServer(string.IsNullOrEmpty(rootPath) ? null : new Uri(rootPath), configuration ?? Default, diagnosticEvents);
        }

        public async Task<Server> CreateServer(Uri rootUri, InterpreterConfiguration configuration = null, Dictionary<Uri, PublishDiagnosticsEventArgs> diagnosticEvents = null) {
            configuration = configuration ?? Default;
            configuration.AssertInstalled();
            var s = new Server();
            s.OnLogMessage += Server_OnLogMessage;
            var properties = new InterpreterFactoryCreationOptions {
                TraceLevel = System.Diagnostics.TraceLevel.Verbose,
                DatabasePath = TestData.GetAstAnalysisCachePath(configuration.Version)
            }.ToDictionary();
            configuration.WriteToDictionary(properties);

            await s.Initialize(new InitializeParams {
                rootUri = rootUri,
                initializationOptions = new PythonInitializationOptions {
                    interpreter = new PythonInitializationOptions.Interpreter {
                        assembly = typeof(AstPythonInterpreterFactory).Assembly.Location,
                        typeName = typeof(AstPythonInterpreterFactory).FullName,
                        properties = properties
                    },
                    testEnvironment = true,
                    analysisUpdates = true,
                    traceLogging = true,
                },
                capabilities = new ClientCapabilities {
                    python = new PythonClientCapabilities {
                        liveLinting = true,
                    }
                }
            }, CancellationToken.None);

            if (diagnosticEvents != null) {
                s.OnPublishDiagnostics += (sender, e) => { lock (diagnosticEvents) diagnosticEvents[e.uri] = e; };
            }

            if (rootUri != null) {
                await LoadFromDirectoryAsync(s, rootUri.LocalPath).ConfigureAwait(false);
                await s.WaitForCompleteAnalysisAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return s;
        }

        private async Task LoadFromDirectoryAsync(Server s, string rootDir) {
            foreach (var dir in PathUtils.EnumerateDirectories(rootDir)) {
                await LoadFromDirectoryAsync(s, dir);
            }
            foreach (var file in PathUtils.EnumerateFiles(rootDir)) {
                if (ModulePath.IsPythonSourceFile(file)) {
                    await s.LoadFileAsync(new Uri(file));
                }
            }
        }

        private void Server_OnLogMessage(object sender, LogMessageEventArgs e) {
            switch (e.type) {
                case MessageType.Error: Trace.TraceError(e.message); break;
                case MessageType.Warning: Trace.TraceWarning(e.message); break;
                case MessageType.Info: Trace.TraceInformation(e.message); break;
                case MessageType.Log: Trace.TraceInformation("LOG: " + e.message); break;
            }
        }

        private TextDocumentIdentifier GetDocument(string file) {
            if (!Path.IsPathRooted(file)) {
                file = TestData.GetPath(file);
            }
            return new TextDocumentIdentifier { uri = new Uri(file) };
        }

        private static async Task<Uri> AddModule(Server s, string content, string moduleName = null, Uri uri = null, string language = null) {
            uri = uri ?? new Uri($"python://test/{moduleName ?? "test-module"}.py");
            await s.DidOpenTextDocument(new DidOpenTextDocumentParams {
                textDocument = new TextDocumentItem {
                    uri = uri,
                    text = content,
                    languageId = language ?? "python"
                }
            }, CancellationToken.None).ConfigureAwait(false);
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None).ConfigureAwait(false);
            return uri;
        }

        [TestMethod, Priority(0)]
        public async Task Initialize() {
            var s = await CreateServer(TestData.GetPath(@"TestData\HelloWorld"));

            var u = GetDocument(@"TestData\HelloWorld\Program.py").uri.AbsoluteUri;
            s.GetLoadedFiles().Should().OnlyContain(u);
        }

        [TestMethod, Priority(0)]
        public async Task OpenFile() {
            var s = await CreateServer(TestData.GetPath(@"TestData\HelloWorld"));

            var u = await AddModule(s, "a = 1", "mod");
            s.GetLoadedFiles().Should().Contain(u.AbsoluteUri);

            (await s.UnloadFileAsync(u)).Should().BeTrue();
            s.GetLoadedFiles().Should().NotContain(u.AbsoluteUri);
        }

        [TestMethod, Priority(0)]
        public async Task ApplyChanges() {
            var s = await CreateServer();

            var m = await AddModule(s, "", "mod");
            Assert.AreEqual(Tuple.Create("x", 1), await ApplyChange(s, m, DocumentChange.Insert("x", new SourceLocation(1, 1))));
            Assert.AreEqual(Tuple.Create("", 2), await ApplyChange(s, m, DocumentChange.Delete(new SourceLocation(1, 1), new SourceLocation(1, 2))));
            Assert.AreEqual(Tuple.Create("y", 3), await ApplyChange(s, m, DocumentChange.Insert("y", new SourceLocation(1, 1))));
        }

        private static Task<Tuple<string, int>> ApplyChange(
            Server s,
            Uri document,
            params DocumentChange[] e
        ) {
            var initialVersion = Math.Max((s.GetEntry(document) as IDocument)?.GetDocumentVersion(s.GetPart(document)) ?? 0, 0);
            return ApplyChange(s, document, initialVersion, initialVersion + 1, e);
        }

        private static async Task<Tuple<string, int>> ApplyChange(
            Server s,
            Uri document,
            int initialVersion,
            int finalVersion,
            params DocumentChange[] e
        ) {

            var parseComplete = EventTaskSources.Server.OnParseComplete.Create(s);

            await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                textDocument = new VersionedTextDocumentIdentifier {
                    uri = document,
                    version = finalVersion,
                },
                contentChanges = e.Select(c => new TextDocumentContentChangedEvent {
                    range = c.WholeBuffer ? null : (Range?)c.ReplacedSpan,
                    text = c.InsertedText
                }).ToArray()
            }, CancellationToken.None);

            await parseComplete;

            var newVersion = -1;
            var code = (s.GetEntry(document) as IDocument)?.ReadDocument(s.GetPart(document), out newVersion).ReadToEnd();
            return Tuple.Create(code, newVersion);
        }

        [TestMethod, Priority(0)]
        public async Task TopLevelCompletions() {
            var s = await CreateServer(TestData.GetPath(@"TestData\AstAnalysis"));

            await AssertCompletion(
                s,
                GetDocument(@"TestData\AstAnalysis\TopLevelCompletions.py"),
                new[] { "x", "y", "z", "int", "float", "class", "def", "while", "in" },
                new[] { "return", "sys", "yield" }
            );

            // Completions in function body
            await AssertCompletion(
                s,
                GetDocument(@"TestData\AstAnalysis\TopLevelCompletions.py"),
                new[] { "x", "y", "z", "int", "float", "class", "def", "while", "in", "return", "yield" },
                new[] { "sys" },
                position: new Position { line = 5, character = 5 }
            );
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInForStatement() {
            var s = await CreateServer();
            Uri u;

            u = await AddModule(s, "for  ");
            await AssertCompletion(s, u, new[] { "for" }, new string[0], new SourceLocation(1, 4));
            await AssertNoCompletion(s, u, new SourceLocation(1, 5));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "for  x ");
            await AssertCompletion(s, u, new[] { "for" }, new string[0], new SourceLocation(1, 4));
            await AssertNoCompletion(s, u, new SourceLocation(1, 5));
            await AssertNoCompletion(s, u, new SourceLocation(1, 6));
            await AssertNoCompletion(s, u, new SourceLocation(1, 7));
            await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 8));
            await s.UnloadFileAsync(u);

            // TODO: Fix parser to parse "for x i" as ForStatement and not ForStatement+ExpressionStatement
            //u = await AddModule(s, "for x i");
            //await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 8), applicableSpan: new SourceSpan(1, 7, 1, 8));
            //await s.UnloadFileAsync(u);

            u = await AddModule(s, "for x in ");
            await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 7));
            await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(1, 9));
            await AssertCompletion(s, u, new[] { "abs", "x" }, new string[0], new SourceLocation(1, 10));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "def f():\n    for ");
            await AssertNoCompletion(s, u, new SourceLocation(2, 9));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "def f():\n    for x in ");
            await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(2, 11));
            await AssertCompletion(s, u, new[] { "in" }, new[] { "for", "abs" }, new SourceLocation(2, 13));
            await AssertCompletion(s, u, new[] { "abs", "x" }, new string[0], new SourceLocation(2, 14));
            await s.UnloadFileAsync(u);

            if (!(this is LanguageServerTests_V2)) {
                u = await AddModule(s, "async def f():\n    async for x in ");
                await AssertCompletion(s, u, new[] { "async", "for" }, new string[0], new SourceLocation(2, 5));
                await AssertCompletion(s, u, new[] { "async", "for" }, new string[0], new SourceLocation(2, 10));
                await AssertCompletion(s, u, new[] { "async", "for" }, new string[0], new SourceLocation(2, 14));
                await AssertNoCompletion(s, u, new SourceLocation(2, 15));
                await s.UnloadFileAsync(u);
            }
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInFunctionDefinition() {
            var s = await CreateServer();
            var u = await AddModule(s, "def f(a, b:int, c=2, d:float=None): pass");

            await AssertNoCompletion(s, u, new SourceLocation(1, 5));
            await AssertNoCompletion(s, u, new SourceLocation(1, 7));
            await AssertNoCompletion(s, u, new SourceLocation(1, 8));
            await AssertNoCompletion(s, u, new SourceLocation(1, 10));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 14));
            await AssertNoCompletion(s, u, new SourceLocation(1, 17));
            await AssertNoCompletion(s, u, new SourceLocation(1, 19));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 29));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 34));
            await AssertNoCompletion(s, u, new SourceLocation(1, 35));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 36));

            if (this is LanguageServerTests_V2) {
                u = await AddModule(s, "@dec\ndef  f(): pass");
                await AssertAnyCompletion(s, u, new SourceLocation(1, 1));
                await AssertCompletion(s, u, new[] { "abs" }, new[] { "def" }, new SourceLocation(1, 2));
                await AssertCompletion(s, u, new[] { "def" }, new string[0], new SourceLocation(2, 1));
                await AssertCompletion(s, u, new[] { "def" }, new string[0], new SourceLocation(2, 4));
                await AssertNoCompletion(s, u, new SourceLocation(2, 5));
                await AssertNoCompletion(s, u, new SourceLocation(2, 6));
            } else {
                u = await AddModule(s, "@dec\nasync   def  f(): pass");
                await AssertAnyCompletion(s, u, new SourceLocation(1, 1));
                await AssertCompletion(s, u, new[] { "abs" }, new[] { "def" }, new SourceLocation(1, 2));
                await AssertCompletion(s, u, new[] { "def" }, new string[0], new SourceLocation(2, 1));
                await AssertCompletion(s, u, new[] { "def" }, new string[0], new SourceLocation(2, 12));
                await AssertNoCompletion(s, u, new SourceLocation(2, 13));
                await AssertNoCompletion(s, u, new SourceLocation(2, 14));
            }
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInClassDefinition() {
            var s = await CreateServer();
            var u = await AddModule(s, "class C(object, parameter=MC): pass");

            await AssertNoCompletion(s, u, new SourceLocation(1, 8));
            if (this is LanguageServerTests_V2) {
                await AssertCompletion(s, u, new[] { "object" }, new[] { "metaclass=" }, new SourceLocation(1, 9));
            } else {
                await AssertCompletion(s, u, new[] { "metaclass=", "object" }, new string[0], new SourceLocation(1, 9));
            }
            await AssertAnyCompletion(s, u, new SourceLocation(1, 15));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 17));
            await AssertCompletion(s, u, new[] { "object" }, new[] { "metaclass=" }, new SourceLocation(1, 29));
            await AssertNoCompletion(s, u, new SourceLocation(1, 30));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 31));

            u = await AddModule(s, "class D(o");
            await AssertNoCompletion(s, u, new SourceLocation(1, 8));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 9));

            u = await AddModule(s, "class E(metaclass=MC,o): pass");
            await AssertCompletion(s, u, new[] { "object" }, new[] { "metaclass=" }, new SourceLocation(1, 22));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInWithStatement() {
            var s = await CreateServer();
            Uri u;

            u = await AddModule(s, "with x as y, z as w: pass");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 6));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
            await AssertNoCompletion(s, u, new SourceLocation(1, 11));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 14));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 17));
            await AssertNoCompletion(s, u, new SourceLocation(1, 20));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 21));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "with ");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 6));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "with x ");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 6));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "with x as ");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 6));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
            await AssertNoCompletion(s, u, new SourceLocation(1, 11));
            await s.UnloadFileAsync(u);
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInImport() {
            var s = await CreateServer();
            var u = await AddModule(s, "import unittest.case as C, unittest\nfrom unittest.case import TestCase as TC, TestCase");

            await AssertCompletion(s, u, new[] { "from", "import", "abs", "dir" }, new[] { "abc" }, new SourceLocation(1, 7));
            await AssertCompletion(s, u, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
            await AssertCompletion(s, u, new[] { "case" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 17));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 22));
            await AssertNoCompletion(s, u, new SourceLocation(1, 25));
            await AssertCompletion(s, u, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(1, 28));

            await AssertCompletion(s, u, new[] { "from", "import", "abs", "dir" }, new[] { "abc" }, new SourceLocation(2, 5));
            await AssertCompletion(s, u, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(2, 6));
            await AssertCompletion(s, u, new[] { "case" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 15));
            await AssertCompletion(s, u, new[] { "import" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 20));
            await AssertCompletion(s, u, new[] { "import" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 22), applicableSpan: new SourceSpan(2, 20, 2, 26));
            await AssertCompletion(s, u, new[] { "TestCase" }, new[] { "abs", "dir", "case" }, new SourceLocation(2, 27));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 36));
            await AssertNoCompletion(s, u, new SourceLocation(2, 39));
            await AssertCompletion(s, u, new[] { "TestCase" }, new[] { "abs", "dir", "case" }, new SourceLocation(2, 44));

            await s.UnloadFileAsync(u);

            u = await AddModule(s, "from unittest.case imp\n\npass");
            await AssertCompletion(s, u, new[] { "import" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 22), applicableSpan: new SourceSpan(1, 20, 1, 23));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "import unittest.case a\n\npass");
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 23), applicableSpan: new SourceSpan(1, 22, 1, 23));
            await s.UnloadFileAsync(u);
            u = await AddModule(s, "from unittest.case import TestCase a\n\npass");
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 37), applicableSpan: new SourceSpan(1, 36, 1, 37));
            await s.UnloadFileAsync(u);
        }

        [TestMethod, Priority(0)]
        public async Task CompletionForOverride() {
            var s = await CreateServer();
            var u = await AddModule(s, "class A(object):\n    def i(): pass\n    def \npass");

            await AssertNoCompletion(s, u, new SourceLocation(2, 9));
            await AssertCompletion(s, u, new[] { "def" }, new[] { "__init__" }, new SourceLocation(3, 8));
            await AssertCompletion(s, u, new[] { "__init__" }, new[] { "def" }, new SourceLocation(3, 9), cmpKey: ci => ci.label);
        }

        [TestMethod, Priority(0)]
        public async Task CompletionForOverrideArgs() {
            using (var s = await CreateServer()) {
                var u = await AddModule(s, "class A:\n    def bar(arg=None): pass\n\nclass B(A):\n    def b");

                await AssertNoCompletion(s, u, new SourceLocation(2, 9));
                await AssertCompletion(s, u, 
                    new[] { "bar(arg=None):\r\n    return super().bar()" },
                    new[] { "bar(arg = None):\r\n    return super().bar()" }, 
                    new SourceLocation(5, 10));
            }
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInDecorator() {
            var s = await CreateServer();
            var u = await AddModule(s, "@dec\ndef f(): pass\n\nx = a @ b");

            await AssertCompletion(s, u, new[] { "f", "x", "property", "abs" }, new[] { "def" }, new SourceLocation(1, 2));
            await AssertCompletion(s, u, new[] { "f", "x", "property", "abs" }, new[] { "def" }, new SourceLocation(4, 8));

            u = await AddModule(s, "@");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 2));

            u = await AddModule(s, "import unittest\n\n@unittest.\n");
            await AssertCompletion(s, u, new[] { "TestCase", "skip", "skipUnless" }, new[] { "abs", "def" }, new SourceLocation(3, 11));

            u = await AddModule(s, "import unittest\n\n@unittest.\ndef f(): pass");
            await AssertCompletion(s, u, new[] { "TestCase", "skip", "skipUnless" }, new[] { "abs", "def" }, new SourceLocation(3, 11));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInRaise() {
            var s = await CreateServer();
            var u = await AddModule(s, "raise ");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(1, 7));
            await s.UnloadFileAsync(u);

            if (!(this is LanguageServerTests_V2)) {
                u = await AddModule(s, "raise Exception from ");
                await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(1, 7));
                await AssertCompletion(s, u, new[] { "from" }, new[] { "Exception", "def", "abs" }, new SourceLocation(1, 17));
                await AssertAnyCompletion(s, u, new SourceLocation(1, 22));
                await s.UnloadFileAsync(u);

                u = await AddModule(s, "raise Exception fr");
                await AssertCompletion(s, u, new[] { "from" }, new[] { "Exception", "def", "abs" }, new SourceLocation(1, 19), applicableSpan: new SourceSpan(1, 17, 1, 19));
                await s.UnloadFileAsync(u);
            }

            u = await AddModule(s, "raise Exception, x, y");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 17));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 20));
            await s.UnloadFileAsync(u);
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInExcept() {
            var s = await CreateServer();
            Uri u;
            u = await AddModule(s, "try:\n    pass\nexcept ");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 8));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "try:\n    pass\nexcept (");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 9));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "try:\n    pass\nexcept Exception  as ");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 8));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "Exception", "def", "abs" }, new SourceLocation(3, 18));
            await AssertNoCompletion(s, u, new SourceLocation(3, 22));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "try:\n    pass\nexc");
            await AssertCompletion(s, u, new[] { "except", "def", "abs" }, new string[0], new SourceLocation(3, 3));
            await s.UnloadFileAsync(u);

            u = await AddModule(s, "try:\n    pass\nexcept Exception a");
            await AssertCompletion(s, u, new[] { "as" }, new[] { "Exception", "def", "abs" }, new SourceLocation(3, 19), applicableSpan: new SourceSpan(3, 18, 3, 19));
            await s.UnloadFileAsync(u);
        }

        [TestMethod, Priority(0)]
        public async Task CompletionAfterDot() {
            var s = await CreateServer();
            Uri u;

            u = await AddModule(s, "x = 1\nx. n\nx.(  )\nx(x.  )\nx.  \nx  ");
            await AssertCompletion(s, u, new[] { "real", "imag" }, new[] { "abs" }, new SourceLocation(2, 3));
            await AssertCompletion(s, u, new[] { "real", "imag" }, new[] { "abs" }, new SourceLocation(2, 4));
            await AssertCompletion(s, u, new[] { "real", "imag" }, new[] { "abs" }, new SourceLocation(2, 5));
            await AssertCompletion(s, u, new[] { "real", "imag" }, new[] { "abs" }, new SourceLocation(3, 3));
            await AssertCompletion(s, u, new[] { "real", "imag" }, new[] { "abs" }, new SourceLocation(4, 5));
            await AssertCompletion(s, u, new[] { "real", "imag" }, new[] { "abs" }, new SourceLocation(5, 4));
            await AssertCompletion(s, u, new[] { "abs" }, new[] { "real", "imag" }, new SourceLocation(6, 2));
            await AssertNoCompletion(s, u, new SourceLocation(6, 3));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionAfterAssign() {
            var s = await CreateServer();
            Uri u;

            u = await AddModule(s, "x = x\ny = ");
            await AssertCompletion(s, u, new[] { "x", "abs" }, null, new SourceLocation(1, 5));
            await AssertCompletion(s, u, new[] { "x", "abs" }, null, new SourceLocation(2, 5));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionWithNewDot() {
            // LSP assumes that the text buffer is up to date with typing,
            // which means the language server must know about dot for a
            // dot completion.
            // To do this, we have to support using a newer tree than the
            // current analysis, so that we can quickly parse the new text
            // with the dot but not block on reanalysis.
            var s = await CreateServer();
            var code = @"
class MyClass:
    def f(self): pass

mc = MyClass()
mc
";
            int testLine = 5;
            int testChar = 2;

            var mod = await AddModule(s, code);

            // Completion after "mc " should normally be blank
            await AssertCompletion(s, mod,
                new string[0],
                new string[0],
                position: new Position { line = testLine, character = testChar + 1 }
            );

            // While we're here, test with the special override field
            await AssertCompletion(s, mod,
                new[] { "f" },
                new[] { "abs", "bin", "int", "mc" },
                position: new Position { line = testLine, character = testChar + 1 },
                expr: "mc"
            );

            // Send the document update.
            await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                textDocument = new VersionedTextDocumentIdentifier { uri = mod, version = 1 },
                contentChanges = new[] { new TextDocumentContentChangedEvent {
                    text = ".",
                    range = new Range {
                        start = new Position { line = testLine, character = testChar },
                        end = new Position { line = testLine, character = testChar }
                    }
                } },
                // Suppress reanalysis to avoid a race
                _enqueueForAnalysis = false
            }, CancellationToken.None);

            // Now with the "." event sent, we should see this as a dot completion
            await AssertCompletion(s, mod,
                new[] { "f" },
                new[] { "abs", "bin", "int", "mc" },
                position: new Position { line = testLine, character = testChar + 1 }
            );
        }

        [TestMethod, Priority(0)]
        public async Task CompletionAfterLoad() {
            var s = await CreateServer();
            var mod1 = await AddModule(s, "import mod2\n\nmod2.", "mod1");

            await AssertCompletion(s, mod1,
                position: new Position { line = 2, character = 5 },
                contains: new string[0],
                excludes: new[] { "value" }
            );

            var mod2 = await AddModule(s, "value = 123", "mod2");

            await AssertCompletion(s, mod1,
                position: new Position { line = 2, character = 5 },
                contains: new[] { "value" },
                excludes: new string[0]
            );

            await s.UnloadFileAsync(mod2);
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None);

            await AssertCompletion(s, mod1,
                position: new Position { line = 2, character = 5 },
                contains: new string[0],
                excludes: new[] { "value" }
            );
        }


        class TestCompletionHookProvider : ILanguageServerExtensionProvider {
            public Task<ILanguageServerExtension> CreateAsync(IPythonLanguageServer server, IReadOnlyDictionary<string, object> properties, CancellationToken cancellationToken) {
                return Task.FromResult<ILanguageServerExtension>(new TestCompletionHook());
            }
        }

        class TestCompletionHook : ILanguageServerExtension, ICompletionExtension {
            public void Dispose() { }

            #region ILanguageServerExtension
            public string Name => "Test completion extension";
            public Task Initialize(IServiceContainer services, CancellationToken token) => Task.CompletedTask;
            public Task<IReadOnlyDictionary<string, object>> ExecuteCommand(string command, IReadOnlyDictionary<string, object> properties, CancellationToken token)
                => Task.FromResult<IReadOnlyDictionary<string, object>>(null);
            #endregion

            #region ICompletionExtension
            public Task HandleCompletionAsync(Uri documentUri, IModuleAnalysis analysis, PythonAst tree, SourceLocation location, CompletionList completions, CancellationToken token) {
                Assert.IsNotNull(tree);
                Assert.IsNotNull(analysis);
                for (int i = 0; i < completions.items.Length; ++i) {
                    completions.items[i].insertText = "*" + completions.items[i].insertText;
                }
                return Task.CompletedTask;
            }
            #endregion
        }

        [TestMethod, Priority(0)]
        public async Task CompletionHook() {
            var s = await CreateServer();
            var u = await AddModule(s, "x = 123\nx.");

            await AssertCompletion(s, u, new[] { "real", "imag" }, new string[0], new Position { line = 1, character = 2 });

            await s.LoadExtensionAsync(new PythonAnalysisExtensionParams {
                assembly = typeof(TestCompletionHookProvider).Assembly.FullName,
                typeName = typeof(TestCompletionHookProvider).FullName
            }, null, CancellationToken.None);

            await AssertCompletion(s, u, new[] { "*real", "*imag" }, new[] { "real" }, new Position { line = 1, character = 2 });
        }

        [TestMethod, Priority(0)]
        public async Task SignatureHelp() {
            var s = await CreateServer();
            var mod = await AddModule(s, @"f()
def f(): pass
def f(a): pass
def f(a, b): pass
def f(a, *b): pass
def f(a, **b): pass
def f(a, *b, **c): pass

");

            await AssertSignature(s, mod, new SourceLocation(1, 3),
                new string[] { "f()", "f(a)", "f(a, b)", "f(a, *b)", "f(a, **b)", "f(a, *b, **c)" },
                new string[0]
            );

            if (Default.Version.Major != 3) {
                return;
            }

            await s.UnloadFileAsync(mod);

            mod = await AddModule(s, @"f()
def f(a : int): pass
def f(a : int, b: int): pass
def f(x : str, y: str): pass
def f(a = 2, b): pass

");

            await AssertSignature(s, mod, new SourceLocation(1, 3),
                new string[] { "f(a: int)", "f(a: int=2, b: int)", "f(x: str, y: str)" },
                new string[0]
            );
        }

        [TestMethod, Priority(0)]
        public async Task Hover() {
            var s = await CreateServer();
            var mod = await AddModule(s, @"123
'abc'
f()
def f(): pass

class C:
    def f(self):
        def g(self):
            pass
        return g

C.f
c = C()
c_g = c.f()

x = 123
x = 3.14
");

            await AssertHover(s, mod, new SourceLocation(1, 1), "int", new[] { "int" }, new SourceSpan(1, 1, 1, 4));
            await AssertHover(s, mod, new SourceLocation(2, 1), "str", new[] { "str" }, new SourceSpan(2, 1, 2, 6));
            await AssertHover(s, mod, new SourceLocation(3, 1), "built-in function test-module.f()", new[] { "test-module.f" }, new SourceSpan(3, 1, 3, 2));
            await AssertHover(s, mod, new SourceLocation(4, 6), "built-in function test-module.f()", new[] { "test-module.f" }, new SourceSpan(4, 5, 4, 6));

            await AssertHover(s, mod, new SourceLocation(12, 1), "class test-module.C", new[] { "test-module.C" }, new SourceSpan(12, 1, 12, 2));
            await AssertHover(s, mod, new SourceLocation(13, 1), "c: C", new[] { "test-module.C" }, new SourceSpan(13, 1, 13, 2));
            await AssertHover(s, mod, new SourceLocation(14, 7), "c: C", new[] { "test-module.C" }, new SourceSpan(14, 7, 14, 8));
            await AssertHover(s, mod, new SourceLocation(14, 9), "c.f: method f of test-module.C objects*", new[] { "test-module.C.f" }, new SourceSpan(14, 7, 14, 10));
            await AssertHover(s, mod, new SourceLocation(14, 1), $"built-in function test-module.C.f.g(self)  {Environment.NewLine}declared in C.f", new[] { "test-module.C.f.g" }, new SourceSpan(14, 1, 14, 4));

            await AssertHover(s, mod, new SourceLocation(16, 1), "x: int, float", new[] { "int", "float" }, new SourceSpan(16, 1, 16, 2));
        }

        [TestMethod, Priority(0)]
        public async Task HoverSpanCheck() {
            var s = await CreateServer();
            var mod = await AddModule(s, @"import datetime
datetime.datetime.now().day
");

            await AssertHover(s, mod, new SourceLocation(2, 1), "built-in module datetime*", new[] { "datetime" }, new SourceSpan(2, 1, 2, 9));
            if (this is LanguageServerTests_V2) {
                await AssertHover(s, mod, new SourceLocation(2, 11), "class datetime.datetime*", new[] { "datetime.datetime" }, new SourceSpan(2, 1, 2, 18));
            } else {
                await AssertHover(s, mod, new SourceLocation(2, 11), "class datetime.datetime*", new[] { "datetime.datetime" }, new SourceSpan(2, 1, 2, 18));
            }
            await AssertHover(s, mod, new SourceLocation(2, 20), "datetime.datetime.now: bound built-in method now*", null, new SourceSpan(2, 1, 2, 22));

            if (!(this is LanguageServerTests_V2)) {
                await AssertHover(s, mod, new SourceLocation(2, 28), "datetime.datetime.now().day: int*", new[] { "int" }, new SourceSpan(2, 1, 2, 28));
            }
        }

        [TestMethod, Priority(0)]
        public async Task FromImportHover() {
            using (var s = await CreateServer()) {
                var mod = await AddModule(s, @"from os import path as p\n");
                await AssertHover(s, mod, new SourceLocation(1, 7), "built-in module os*", null, new SourceSpan(1, 6, 1, 8));
                await AssertHover(s, mod, new SourceLocation(1, 17), "built-in module posixpath*", new[] { "posixpath" }, new SourceSpan(1, 16, 1, 20));
                await AssertHover(s, mod, new SourceLocation(1, 25), "built-in module posixpath*", new[] { "posixpath" }, new SourceSpan(1, 24, 1, 25));
            }
        }

        [TestMethod, Priority(0)]
        public async Task FromImportRelativeHover() {
            using (var s = await CreateServer()) {
                var mod1 = await AddModule(s, @"from . import mod2\n", "mod1");
                var mod2 = await AddModule(s, @"def foo():\n  pass\n", "mod2");
                await AssertHover(s, mod1, new SourceLocation(1, 16), "built-in module mod2", null, new SourceSpan(1, 15, 1, 19));
            }
        }

        [TestMethod, Priority(0)]
        public async Task MultiPartDocument() {
            var s = await CreateServer();

            var mod = await AddModule(s, "x = 1", "mod");
            var modP2 = new Uri(mod, "#2");
            var modP3 = new Uri(mod, "#3");

            await AssertCompletion(s, mod, new[] { "x" }, null);

            Assert.AreEqual(Tuple.Create("y = 2", 1), await ApplyChange(s, modP2, DocumentChange.Insert("y = 2", SourceLocation.MinValue)));
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None);

            await AssertCompletion(s, modP2, new[] { "x", "y" }, null);

            Assert.AreEqual(Tuple.Create("z = 3", 1), await ApplyChange(s, modP3, DocumentChange.Insert("z = 3", SourceLocation.MinValue)));
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None);

            await AssertCompletion(s, modP3, new[] { "x", "y", "z" }, null);
            await AssertCompletion(s, mod, new[] { "x", "y", "z" }, null);

            await ApplyChange(s, mod, DocumentChange.Delete(SourceLocation.MinValue, SourceLocation.MinValue.AddColumns(5)));
            await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
            await AssertCompletion(s, modP2, new[] { "y", "z" }, new[] { "x" });
            await AssertCompletion(s, modP3, new[] { "y", "z" }, new[] { "x" });
        }

        [TestMethod, Priority(0)]
        public async Task UpdateDocumentBuffer() {
            var s = await CreateServer();

            var mod = await AddModule(s, "");

            Assert.AreEqual(Tuple.Create("test", 1), await ApplyChange(s, mod, DocumentChange.Insert("test", SourceLocation.MinValue)));
            Assert.AreEqual(Tuple.Create("", 0), await ApplyChange(s, mod, 1, 0, new DocumentChange { WholeBuffer = true }));
            Assert.AreEqual(Tuple.Create("test", 1), await ApplyChange(s, mod, DocumentChange.Insert("test", SourceLocation.MinValue)));
        }

        [TestMethod, Priority(0)]
        public async Task ParseErrorDiagnostics() {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            var s = await CreateServer((string)null, null, diags);
            var u = await AddModule(s, "def f(/)\n    error text\n");

            GetDiagnostics(diags, u).Should().OnlyContain(
                "Error;unexpected token '/';Python (parser);0;6;7",
                "Error;invalid parameter;Python (parser);0;6;7",
                "Error;unexpected token '<newline>';Python (parser);0;8;4",
                "Error;unexpected indent;Python (parser);1;4;9",
                "Error;unexpected token 'text';Python (parser);1;10;14",
                "Error;unexpected token '<dedent>';Python (parser);1;14;0"
            );
        }

        [TestMethod, Priority(0)]
        public async Task ParseIndentationDiagnostics() {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            var s = await CreateServer((string)null, null, diags);

            foreach (var tc in new[] {
                DiagnosticSeverity.Error,
                DiagnosticSeverity.Warning,
                DiagnosticSeverity.Information,
                DiagnosticSeverity.Unspecified
            }) {
                // For now, these options have to be configured directly
                s.ParseQueue.InconsistentIndentation = tc;

                Trace.TraceInformation("Testing {0}", tc);

                var mod = await AddModule(s, "");
                await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                    contentChanges = new[] {
                            new TextDocumentContentChangedEvent {
                                text = "def f():\r\n        pass\r\n\tpass"
                            }
                        },
                    textDocument = new VersionedTextDocumentIdentifier { uri = mod, version = 2 }
                }, CancellationToken.None);
                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);

                var messages = GetDiagnostics(diags, mod).ToArray();
                if (tc == DiagnosticSeverity.Unspecified) {
                    messages.Should().BeEmpty();
                } else {
                    messages.Should().OnlyContain($"{tc};inconsistent whitespace;Python (parser);2;0;1");
                }

                await s.UnloadFileAsync(mod);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ParseAndAnalysisDiagnostics() {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            var s = await CreateServer((Uri)null, null, diags);

            var u = await AddModule(s, "y\nx x");

            GetDiagnostics(diags, u).Should().OnlyContain(
                "Warning;unknown variable 'y';Python (analysis);0;0;1",
                "Warning;unknown variable 'x';Python (analysis);1;0;1",
                "Error;unexpected token 'x';Python (parser);1;2;3"
            );
        }

        [TestMethod, Priority(0)]
        public async Task OnTypeFormattingLine() {
            using (var s = await CreateServer()) {
                var uri = await AddModule(s, "def foo  ( ) :\n    x = a + b\n    x+= 1");

                // Extended tests for line formatting are in LineFormatterTests.
                // These just verify that the language server formats and returns something correct.
                var edits = await s.SendDocumentOnTypeFormatting(uri, new SourceLocation(2, 1), "\n");
                edits.Should().OnlyHaveTextEdit("def foo():", (0, 0, 0, 14));

                edits = await s.SendDocumentOnTypeFormatting(uri, new SourceLocation(3, 1), "\n");
                edits.Should().OnlyHaveTextEdit("x = a + b", (1, 4, 1, 13));

                edits = await s.SendDocumentOnTypeFormatting(uri, new SourceLocation(4, 1), "\n");
                edits.Should().OnlyHaveTextEdit("x += 1", (2, 4, 2, 9));
            }
        }

        [TestMethod, Priority(0)]
        public async Task OnTypeFormattingBlock() {
            using (var s = await CreateServer()) {
                var uri = await AddModule(s, "if x:\n    pass\n    else:");

                var edits = await s.SendDocumentOnTypeFormatting(uri, new SourceLocation(3, 9), ":");
                edits.Should().OnlyHaveTextEdit("", (2, 0, 2, 4));
            }
        }

        class GetAllExtensionProvider : ILanguageServerExtensionProvider {
            public Task<ILanguageServerExtension> CreateAsync(IPythonLanguageServer server, IReadOnlyDictionary<string, object> properties, CancellationToken cancellationToken) {
                return Task.FromResult<ILanguageServerExtension>(new GetAllExtension((Server)server, properties));
            }

            private class GetAllExtension : ILanguageServerExtension {
                private readonly BuiltinTypeId _typeId;
                private readonly Server _server;

                public GetAllExtension(Server server, IReadOnlyDictionary<string, object> properties) {
                    _server = server;
                    if (!Enum.TryParse((string)properties["typeid"], out _typeId)) {
                        throw new ArgumentException("typeid was not valid");
                    }
                }

                public string Name => "getall";

                public void Dispose() { }

                public Task<IReadOnlyDictionary<string, object>> ExecuteCommand(string command, IReadOnlyDictionary<string, object> properties, CancellationToken token) {
                    if (properties == null) {
                        return null;
                    }

                    // Very bad code, but good for testing. Copy/paste at your own risk!
                    var entry = _server.GetEntry(new Uri((string)properties["uri"])) as IPythonProjectEntry;
                    var location = new SourceLocation((int)properties["line"], (int)properties["column"]);

                    if (command == _typeId.ToString()) {
                        var res = new List<string>();
                        foreach (var m in entry.Analysis.GetAllMembers(location)) {
                            if (m.Values.Any(v => v.MemberType == PythonMemberType.Constant && v.TypeId == _typeId)) {
                                res.Add(m.Name);
                            }
                        }
                        return Task.FromResult<IReadOnlyDictionary<string, object>>(new Dictionary<string, object> { ["names"] = res });
                    }
                    return Task.FromResult<IReadOnlyDictionary<string, object>>(null);
                }

                public Task Initialize(IServiceContainer services, CancellationToken token) => Task.CompletedTask;
            }
        }

        [TestMethod, Priority(0)]
        public async Task ExtensionCommand() {
            var s = await CreateServer();
            var u = await AddModule(s, "x = 1\ny = 2\nz = 'abc'");

            await s.LoadExtensionAsync(new PythonAnalysisExtensionParams {
                assembly = typeof(GetAllExtensionProvider).Assembly.FullName,
                typeName = typeof(GetAllExtensionProvider).FullName,
                properties = new Dictionary<string, object> { ["typeid"] = BuiltinTypeId.Int.ToString() }
            }, null, CancellationToken.None);

            var cmd = new ExtensionCommandParams {
                extensionName = "getall",
                command = "Int",
                properties = new Dictionary<string, object> { ["uri"] = u.AbsoluteUri, ["line"] = 1, ["column"] = 1 }
            };

            var res = (await s.ExtensionCommand(cmd, CancellationToken.None)).properties?["names"];
            res.Should().BeOfType<List<string>>().Which.Should().OnlyContain("x", "y");

            cmd.command = BuiltinTypeId_Str.ToString();
            res = (await s.ExtensionCommand(cmd, CancellationToken.None)).properties?["names"];
            res.Should().BeNull();

            await s.LoadExtensionAsync(new PythonAnalysisExtensionParams {
                assembly = typeof(GetAllExtensionProvider).Assembly.FullName,
                typeName = typeof(GetAllExtensionProvider).FullName,
                properties = new Dictionary<string, object> { ["typeid"] = BuiltinTypeId_Str.ToString() }
            }, null, CancellationToken.None);

            cmd.command = BuiltinTypeId_Str.ToString();
            res = (await s.ExtensionCommand(cmd, CancellationToken.None)).properties?["names"];
            res.Should().BeOfType<List<string>>().Which.Should().Contain("z", "__name__", "__file__");

            cmd.command = "Int";
            res = (await s.ExtensionCommand(cmd, CancellationToken.None)).properties?["names"];
            res.Should().BeNull();
        }

        private static IEnumerable<string> GetDiagnostics(Dictionary<Uri, PublishDiagnosticsEventArgs> events, Uri uri) {
            return events[uri].diagnostics
                .OrderBy(d => (SourceLocation)d.range.start)
                .Select(d => $"{d.severity};{d.message};{d.source};{d.range.start.line};{d.range.start.character};{d.range.end.character}");
        }

        public static async Task AssertCompletion(Server s, TextDocumentIdentifier document, IReadOnlyCollection<string> contains, IReadOnlyCollection<string> excludes, Position? position = null, CompletionContext? context = null, Func<CompletionItem, string> cmpKey = null, string expr = null, Range? applicableSpan = null) {
            var res = await s.Completion(new CompletionParams {
                textDocument = document,
                position = position ?? new Position(),
                context = context,
                _expr = expr
            }, CancellationToken.None);
            DumpDetails(res);

            cmpKey = cmpKey ?? (c => c.insertText);
            var items = res.items?.Select(cmpKey).ToList() ?? new List<string>();

            if (contains != null && contains.Any()) {
                items.Should().Contain(contains);
            }

            if (excludes != null && excludes.Any()) {
                items.Should().NotContain(excludes);
            }

            if (applicableSpan.HasValue) {
                res._applicableSpan.Should().Be(applicableSpan);
            }
        }

        private static void DumpDetails(CompletionList completions) {
            var span = ((SourceSpan?)completions._applicableSpan) ?? SourceSpan.None;
            Debug.WriteLine($"Completed {completions._expr ?? "(null)"} at {span}");
        }

        private static async Task AssertAnyCompletion(Server s, TextDocumentIdentifier document, Position position) {
            var res = await s.Completion(new CompletionParams { textDocument = document, position = position }, CancellationToken.None);
            DumpDetails(res);
            if (res.items == null || !res.items.Any()) {
                Assert.Fail("Completions were not returned");
            }
        }

        private static async Task AssertNoCompletion(Server s, TextDocumentIdentifier document, Position position) {
            var res = await s.Completion(new CompletionParams { textDocument = document, position = position }, CancellationToken.None);
            DumpDetails(res);
            if (res.items != null && res.items.Any()) {
                var msg = string.Join(", ", res.items.Select(c => c.label).Ordered());
                Assert.Fail("Completions were returned: " + msg);
            }
        }

        public static async Task AssertSignature(Server s, TextDocumentIdentifier document, SourceLocation position, IReadOnlyCollection<string> contains, IReadOnlyCollection<string> excludes, string expr = null) {
            var sigs = (await s.SignatureHelp(new TextDocumentPositionParams {
                textDocument = document,
                position = position,
                _expr = expr
            }, CancellationToken.None)).signatures;

            var labels = sigs.Select(sig => sig.label).ToList();


            if (contains != null && contains.Any()) {
                labels.Should().Contain(contains);
            }

            if (excludes != null && excludes.Any()) {
                labels.Should().NotContain(excludes);
            }
        }

        public static async Task AssertHover(Server s, TextDocumentIdentifier document, SourceLocation position, string hoverText, IEnumerable<string> typeNames, SourceSpan? range = null, string expr = null) {
            var hover = await s.Hover(new TextDocumentPositionParams {
                textDocument = document,
                position = position,
                _expr = expr
            }, CancellationToken.None);

            if (hoverText.EndsWith("*")) {
                // Check prefix first, but then show usual message for mismatched value
                if (!hover.contents.value.StartsWith(hoverText.Remove(hoverText.Length - 1))) {
                    Assert.AreEqual(hoverText, hover.contents.value);
                }
            } else {
                Assert.AreEqual(hoverText, hover.contents.value);
            }
            if (typeNames != null) {
                hover._typeNames.Should().OnlyContain(typeNames.ToArray());
            }
            if (range.HasValue) {
                hover.range.Should().Be((Range?)range);
            }
        }

        public Task<ILanguageServerExtension> CreateAsync(IPythonLanguageServer server, IReadOnlyDictionary<string, object> properties, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    [TestClass]
    public class LanguageServerTests_V2 : LanguageServerTests {
        protected override InterpreterConfiguration Default => DefaultV2;
        protected override BuiltinTypeId BuiltinTypeId_Str => BuiltinTypeId.Bytes;
    }
}
