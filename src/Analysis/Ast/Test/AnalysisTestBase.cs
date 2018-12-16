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

using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Tests;
using Microsoft.Python.Parsing.Tests;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    public abstract class AnalysisTestBase {
        protected ServiceManager ServiceManager { get; }
        protected TestLogger TestLogger { get; } = new TestLogger();

        protected AnalysisTestBase(ServiceManager sm = null) {
            ServiceManager = sm ?? new ServiceManager();

            var platform = new OSPlatform();
            ServiceManager
                .AddService(TestLogger)
                .AddService(platform)
                .AddService(new FileSystem(platform));
        }

        protected string GetAnalysisTestDataFilesPath() => TestData.GetPath(Path.Combine("TestData", "AstAnalysis"));

        internal async Task<IServiceContainer> CreateServicesAsync(string root, InterpreterConfiguration configuration = null) {
            configuration = configuration ?? PythonVersions.LatestAvailable;
            configuration.AssertInstalled();
            Trace.TraceInformation("Cache Path: " + configuration.ModuleCachePath);
            configuration.ModuleCachePath = TestData.GetAstAnalysisCachePath(configuration.Version, true);
            configuration.SearchPaths = new[] { root, GetAnalysisTestDataFilesPath() };
            configuration.TypeshedPath = TestData.GetDefaultTypeshedPath();

            var dependencyResolver = new TestDependencyResolver();
            ServiceManager.AddService(dependencyResolver);

            var analyzer = new PythonAnalyzer(ServiceManager);
            ServiceManager.AddService(analyzer);

            var documentTable = new RunningDocumentTable(root, ServiceManager);
            ServiceManager.AddService(documentTable);

            var interpreter = await PythonInterpreter.CreateAsync(configuration, ServiceManager);
            ServiceManager.AddService(interpreter);

            return ServiceManager;
        }

        internal async Task<IDocumentAnalysis> GetAnalysisAsync(string code, InterpreterConfiguration configuration = null, string moduleName = null, string modulePath = null) {

            var moduleUri = TestData.GetDefaultModuleUri();
            modulePath = modulePath ?? TestData.GetDefaultModulePath();
            moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDirectory = Path.GetDirectoryName(modulePath);

            var services = await CreateServicesAsync(moduleDirectory, configuration);
            var mco = new ModuleCreationOptions {
                ModuleName = moduleName,
                Content = code,
                FilePath = modulePath,
                Uri = moduleUri,
                ModuleType = ModuleType.User,
                LoadOptions = ModuleLoadOptions.Analyze
            };
            var doc = new PythonModule(mco, services);

            var ast = await doc.GetAstAsync(CancellationToken.None);
            ast.Should().NotBeNull();

            var analysis = await doc.GetAnalysisAsync(CancellationToken.None);
            analysis.Should().NotBeNull();

            return analysis;
        }

        private sealed class TestDependencyResolver : IDependencyResolver {
            public Task<IDependencyChainNode> GetDependencyChainAsync(IDocument document, CancellationToken cancellationToken)
                => Task.FromResult<IDependencyChainNode>(new DependencyChainNode(document));
        }
    }
}
