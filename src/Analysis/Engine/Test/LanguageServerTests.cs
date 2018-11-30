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
// MERCHANTABILITY OR NON-INFRINGEMENT.
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
using Microsoft.Python.Core.Interpreter;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
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
            uri = uri ?? new Uri($"python://test/{moduleName ?? "test_module"}.py");
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
            var s = await CreateServer(TestData.GetPath(Path.Combine("TestData", "HelloWorld")));

            var u = GetDocument(Path.Combine("TestData", "HelloWorld", "Program.py")).uri.AbsoluteUri;
            s.GetLoadedFiles().Should().OnlyContain(u);
        }

        [TestMethod, Priority(0)]
        public async Task OpenFile() {
            var s = await CreateServer(TestData.GetPath(Path.Combine("TestData", "HelloWorld")));

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
                "Error;unexpected token '/';Python;0;6;7",
                "Error;invalid parameter;Python;0;6;7",
                "Error;unexpected token '<newline>';Python;0;8;4",
                "Error;unexpected indent;Python;1;4;9",
                "Error;unexpected token 'text';Python;1;10;14",
                "Error;unexpected token '<dedent>';Python;1;14;0"
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
                                text = $"def f():{Environment.NewLine}        pass{Environment.NewLine}\tpass"
                            }
                        },
                    textDocument = new VersionedTextDocumentIdentifier { uri = mod, version = 2 }
                }, CancellationToken.None);
                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);

                var messages = GetDiagnostics(diags, mod).ToArray();
                if (tc == DiagnosticSeverity.Unspecified) {
                    messages.Should().BeEmpty();
                } else {
                    messages.Should().OnlyContain($"{tc};inconsistent whitespace;Python;2;0;1");
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
                "Warning;'y' used before definition;Python;0;0;1",
                "Warning;'x' used before definition;Python;1;0;1",
                "Error;unexpected token 'x';Python;1;2;3"
            );
        }

        [TestMethod, Priority(0)]
        public async Task DiagnosticsSettingChange() {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();

            using (var s = await CreateServer((Uri)null, null, diags)) {
                var u = await s.OpenDefaultDocumentAndGetUriAsync("import foo\nx = y");

                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
                GetDiagnostics(diags, u).Should().OnlyContain(
                    "Warning;unresolved import 'foo';Python;0;7;10",
                    "Warning;'y' used before definition;Python;1;4;5"
                );

                var newSettings = new ServerSettings();
                newSettings.analysis.SetErrorSeverityOptions(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new[] { ErrorMessages.UseBeforeDefCode, ErrorMessages.UnresolvedImportCode });
                await s.SendDidChangeConfiguration(newSettings);

                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
                GetDiagnostics(diags, u).Where(st => !st.StartsWith($"{DiagnosticSeverity.Unspecified}")).Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public async Task TypeHintNoneDiagnostic() {
            if (this is LanguageServerTests_V2) {
                // No type hints in Python 2.
                return;
            }

            var code = @"
def f(b: None) -> None:
    b: None
";

            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            using (var s = await CreateServer((Uri)null, null, diags)) {
                var u = await s.OpenDefaultDocumentAndGetUriAsync(code);

                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
                GetDiagnostics(diags, u).Should().BeEmpty();
            }
        }

        [DataRow("mylist = [1, 2, 3, 4]\nx = [[a for a in range(item)] for item in mylist]")]
        [DataRow("mydict = {1: 2, 3: 4}\nx = [[a for a in range(v)] for k, v in mydict.items()]")]
        [DataRow("myset = set([1, 2, 3, 4])\nx = [[a for a in range(item)] for item in myset]")]
        [DataRow("mydict = {1: 2, 3: 4}\nx = {k: [a for a in range(v)] for k, v in mydict.items()}")]
        [DataTestMethod, Priority(0)]
        public async Task NestedComprehensionDiagnostic(string code) {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            using (var s = await CreateServer((Uri)null, null, diags)) {
                var u = await s.OpenDefaultDocumentAndGetUriAsync(code);

                await s.WaitForCompleteAnalysisAsync(CancellationToken.None);
                GetDiagnostics(diags, u).Should().BeEmpty();
            }
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

        private static void DumpDetails(CompletionList completions) {
            var span = ((SourceSpan?)completions._applicableSpan) ?? SourceSpan.None;
            Debug.WriteLine($"Completed {completions._expr ?? "(null)"} at {span}");
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

        public Task<ILanguageServerExtension> CreateAsync(IPythonLanguageServer server, IReadOnlyDictionary<string, object> properties, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    [TestClass]
    public class LanguageServerTests_V2 : LanguageServerTests {
        protected override InterpreterConfiguration Default => DefaultV2;
        protected override BuiltinTypeId BuiltinTypeId_Str => BuiltinTypeId.Bytes;
    }
}
