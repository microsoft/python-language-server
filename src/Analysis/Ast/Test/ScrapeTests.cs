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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ScrapeTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task SpecialFloats() {
            var analysis = await GetAnalysisAsync("import math; inf = math.inf; nan = math.nan", PythonVersions.LatestAvailable3X);

            analysis.Should().HaveVariable("math")
                .And.HaveVariable("inf").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("nan").OfType(BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public async Task CompiledBuiltinScrapeV38x64() => await CompiledBuiltinScrapeAsync(PythonVersions.Python38_x64);

        [TestMethod, Priority(0)]
        public async Task CompiledBuiltinScrapeV37x64() => await CompiledBuiltinScrapeAsync(PythonVersions.Python37_x64);

        [TestMethod, Priority(0)]
        public async Task CompiledBuiltinScrapeV36x64() => await CompiledBuiltinScrapeAsync(PythonVersions.Python36_x64);

        [TestMethod, Priority(0)]
        public async Task CompiledBuiltinScrapeV27x64() => await CompiledBuiltinScrapeAsync(PythonVersions.Python27_x64);
        [TestMethod, Priority(0)]
        public async Task CompiledBuiltinScrapeV27x86() => await CompiledBuiltinScrapeAsync(PythonVersions.Python27);

        private async Task CompiledBuiltinScrapeAsync(InterpreterConfiguration configuration) {
            configuration.AssertInstalled();

            var moduleUri = TestData.GetDefaultModuleUri();
            var moduleDirectory = Path.GetDirectoryName(moduleUri.LocalPath);

            var services = await CreateServicesAsync(moduleDirectory, configuration);
            var interpreter = services.GetService<IPythonInterpreter>();
            
            // TODO: this is Windows only
            var dllsDir = Path.Combine(Path.GetDirectoryName(interpreter.Configuration.LibraryPath), "DLLs");
            if (!Directory.Exists(dllsDir)) {
                Assert.Inconclusive("Configuration does not have DLLs");
            }

            var report = new List<string>();
            var permittedImports = interpreter.LanguageVersion.Is2x() ?
                new[] { interpreter.ModuleResolution.BuiltinModuleName, "exceptions" } :
                new[] { interpreter.ModuleResolution.BuiltinModuleName };

            foreach (var pyd in PathUtils.EnumerateFiles(dllsDir, "*", recurse: false).Where(ModulePath.IsPythonFile)) {
                var mp = ModulePath.FromFullPath(pyd);
                if (mp.IsDebug) {
                    continue;
                }

                Console.WriteLine("Importing {0} from {1}", mp.ModuleName, mp.SourceFile);
                var mod = await interpreter.ModuleResolution.ImportModuleAsync(mp.ModuleName);
                Assert.IsInstanceOfType(mod, typeof(CompiledPythonModule));

                var modPath = interpreter.ModuleResolution.ModuleCache.GetCacheFilePath(pyd);
                Assert.IsTrue(File.Exists(modPath), "No cache file created");
                var moduleCache = File.ReadAllText(modPath);

                var doc = (IDocument)mod;
                var ast = await doc.GetAstAsync();

                var errors = doc.GetDiagnostics();
                foreach (var err in errors) {
                    Console.WriteLine(err);
                }
                Assert.AreEqual(0, errors.Count(), "Parse errors occurred");


                var imports = ((SuiteStatement)ast.Body).Statements
                    .OfType<ImportStatement>()
                    .SelectMany(s => s.Names)
                    .Select(n => n.MakeString())
                    .Except(permittedImports)
                    .ToArray();

                // We expect no imports (after excluding builtins)
                report.AddRange(imports.Select(n => $"{mp.ModuleName} imported {n}"));
            }

            report.Should().BeEmpty();
        }
    }
}
