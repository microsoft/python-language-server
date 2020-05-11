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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Tests;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    public abstract class AnalysisTestBase: IDisposable {
        private readonly CancellationTokenSource _testCts;

        protected TimeSpan AnalysisTimeout { get; set; } = TimeSpan.FromMilliseconds(3000 * 60);

        protected TestLogger TestLogger { get; } = new TestLogger();
        protected ServiceManager Services { get; private set; }

        protected virtual IDiagnosticsService GetDiagnosticsService(IServiceContainer s) => null;

        protected CancellationToken TestCancellationToken => _testCts.Token;

        protected AnalysisTestBase() {
            _testCts = new CancellationTokenSource(Debugger.IsAttached ? Timeout.InfiniteTimeSpan : AnalysisTimeout);
        }

        public void Dispose() {
            _testCts.Dispose();
        }

        protected ServiceManager CreateServiceManager() {
            Services = new ServiceManager();

            var platform = new OSPlatform();
            Services
                .AddService(TestLogger)
                .AddService(platform)
                .AddService(new ProcessServices())
                .AddService(new FileSystem(platform));

            return Services;
        }

        protected string GetAnalysisTestDataFilesPath() => TestData.GetPath("TestData", "AstAnalysis");

        protected Task<IServiceManager> CreateServicesAsync(InterpreterConfiguration configuration, string[] searchPaths = null)
            => CreateServicesAsync(TestData.GetTestSpecificRootPath(), configuration, null, null, searchPaths);

        protected async Task<IServiceManager> CreateServicesAsync(string root, InterpreterConfiguration configuration, string stubCacheFolderPath = null, IServiceManager sm = null, string[] searchPaths = null) {
            configuration = configuration ?? PythonVersions.LatestAvailable;
            configuration.AssertInstalled();
            Trace.TraceInformation("Cache Path: " + stubCacheFolderPath);

            searchPaths = searchPaths ?? new[] { GetAnalysisTestDataFilesPath() };
            var typeshedPath = TestData.GetDefaultTypeshedPath();

            sm = sm ?? CreateServiceManager();

            sm.AddService(Substitute.For<IClientApplication>())
              .AddService(Substitute.For<IIdleTimeService>());

            var ds = GetDiagnosticsService(Services);
            if (ds != null) {
                sm.AddService(ds);
            }

            var ap = Substitute.For<IAnalysisOptionsProvider>();
            ap.Options.Returns(x => new AnalysisOptions {
                AnalysisCachingLevel = AnalysisCachingLevel.None,
                StubOnlyAnalysis = false,
                LintingEnabled = true
            });
            sm.AddService(ap);

            TestLogger.Log(TraceEventType.Information, "Create PythonAnalyzer");

            CacheService.Register(sm, stubCacheFolderPath, pathCheck: false);
            var analyzer = new PythonAnalyzer(sm);
            sm.AddService(analyzer);

            TestLogger.Log(TraceEventType.Information, "Create PythonInterpreter");
            var interpreter = await PythonInterpreter.CreateAsync(configuration, root, sm, typeshedPath, searchPaths.ToImmutableArray());
            sm.AddService(interpreter);

            TestLogger.Log(TraceEventType.Information, "Create RunningDocumentTable");
            sm.AddService(new RunningDocumentTable(sm));

            return sm;
        }

        protected async Task CreateServicesAsync(InterpreterConfiguration configuration, string modulePath) {
            modulePath = modulePath ?? TestData.GetDefaultModulePath();
            var moduleDirectory = Path.GetDirectoryName(modulePath);
            await CreateServicesAsync(moduleDirectory, configuration);
        }

        protected Task<IDocumentAnalysis> GetAnalysisAsync(string code, PythonLanguageVersion version, IServiceManager sm = null, string modulePath = null)
            => GetAnalysisAsync(code, PythonVersions.GetRequiredCPythonConfiguration(version), sm, modulePath);

        protected async Task<IDocumentAnalysis> GetAnalysisAsync(
            string code,
            InterpreterConfiguration configuration = null,
            IServiceManager sm = null,
            string modulePath = null) {

            modulePath = modulePath ?? TestData.GetDefaultModulePath();
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDirectory = Path.GetDirectoryName(modulePath);

            var services = await CreateServicesAsync(moduleDirectory, configuration, null, sm);
            return await GetAnalysisAsync(code, services, moduleName, modulePath);
        }

        protected async Task<IDocumentAnalysis> GetNextAnalysisAsync(string code, string modulePath = null) {
            modulePath = modulePath ?? TestData.GetNextModulePath();
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            return await GetAnalysisAsync(code, Services, moduleName, modulePath);
        }

        protected async Task<IDocumentAnalysis> GetAnalysisAsync(
            string code,
            IServiceContainer services,
            string moduleName = null,
            string modulePath = null) {

            var moduleUri = modulePath != null ? new Uri(modulePath) : TestData.GetDefaultModuleUri();
            modulePath = modulePath ?? TestData.GetDefaultModulePath();
            moduleName = moduleName ?? Path.GetFileNameWithoutExtension(modulePath);

            IDocument doc;
            var rdt = services.GetService<IRunningDocumentTable>();
            if (rdt != null) {
                doc = rdt.OpenDocument(moduleUri, code, modulePath);
            } else {
                var mco = new ModuleCreationOptions {
                    ModuleName = moduleName,
                    Content = code,
                    FilePath = modulePath,
                    Uri = moduleUri,
                    ModuleType = ModuleType.User
                };
                doc = new PythonModule(mco, services);
            }

            TestLogger.Log(TraceEventType.Information, "Test: AST begin.");
            var ast = await doc.GetAstAsync(CancellationToken.None);
            ast.Should().NotBeNull();
            TestLogger.Log(TraceEventType.Information, "Test: AST end.");
            TestLogger.Log(TraceEventType.Information, "Test: Analysis begin.");

            await services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync(TestCancellationToken);
            var analysis = await doc.GetAnalysisAsync(-1, TestCancellationToken);

            analysis.Should().NotBeNull();
            TestLogger.Log(TraceEventType.Information, "Test: Analysis end.");

            return analysis;
        }

        protected async Task<IDocumentAnalysis> GetDocumentAnalysisAsync(IDocument document) {
            var analyzer = Services.GetService<IPythonAnalyzer>();
            await analyzer.WaitForCompleteAnalysisAsync(TestCancellationToken);
            return await document.GetAnalysisAsync(Timeout.Infinite, TestCancellationToken);
        }
    }
}
