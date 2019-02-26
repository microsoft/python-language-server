﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Modules.Resolution;
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

                Console.WriteLine(@"Importing {0} from {1}", mp.ModuleName, mp.SourceFile);
                var mod = interpreter.ModuleResolution.GetOrLoadModule(mp.ModuleName);
                await mod.LoadAndAnalyzeAsync();
                Assert.IsInstanceOfType(mod, typeof(CompiledPythonModule));

                await ((ModuleCache)interpreter.ModuleResolution.ModuleCache).CacheWritingTask;
                var modPath = interpreter.ModuleResolution.ModuleCache.GetCacheFilePath(pyd);
                Assert.IsTrue(File.Exists(modPath), "No cache file created");

                var doc = (IDocument)mod;
                var ast = await doc.GetAstAsync();

                var errors = doc.GetParseErrors().ToArray();
                foreach (var err in errors) {
                    Console.WriteLine(err);
                }
                Assert.AreEqual(0, errors.Length, "Parse errors occurred");


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

        [TestMethod, Priority(0)]
        public async Task BuiltinScrapeV38() => await BuiltinScrape(PythonVersions.Python38_x64 ?? PythonVersions.Python38);

        [TestMethod, Priority(0)]
        public async Task BuiltinScrapeV37() => await BuiltinScrape(PythonVersions.Python37_x64 ?? PythonVersions.Python37);

        [TestMethod, Priority(0)]
        public async Task BuiltinScrapeV36() => await BuiltinScrape(PythonVersions.Python36_x64 ?? PythonVersions.Python36);

        [TestMethod, Priority(0)]
        public async Task BuiltinScrapeV27() => await BuiltinScrape(PythonVersions.Python27_x64 ?? PythonVersions.Python27);

        private async Task BuiltinScrape(InterpreterConfiguration configuration) {
            configuration.AssertInstalled();
            var moduleUri = TestData.GetDefaultModuleUri();
            var moduleDirectory = Path.GetDirectoryName(moduleUri.LocalPath);

            var services = await CreateServicesAsync(moduleDirectory, configuration);
            var interpreter = services.GetService<IPythonInterpreter>();

            var mod = interpreter.ModuleResolution.GetOrLoadModule(interpreter.ModuleResolution.BuiltinModuleName);
            await mod.LoadAndAnalyzeAsync(new CancellationTokenSource(5000).Token);
            Assert.IsInstanceOfType(mod, typeof(BuiltinsPythonModule));
            var modPath = interpreter.ModuleResolution.ModuleCache.GetCacheFilePath(interpreter.Configuration.InterpreterPath);

            var doc = mod as IDocument;
            var errors = doc.GetParseErrors().ToArray();
            foreach (var err in errors) {
                Console.WriteLine(err);
            }
            Assert.AreEqual(0, errors.Count(), "Parse errors occurred");

            var ast = await doc.GetAstAsync();
            var seen = new HashSet<string>();
            foreach (var stmt in ((SuiteStatement)ast.Body).Statements) {
                if (stmt is ClassDefinition cd) {
                    Assert.IsTrue(seen.Add(cd.Name), $"Repeated use of {cd.Name} at index {cd.StartIndex} in {modPath}");
                } else if (stmt is FunctionDefinition fd) {
                    Assert.IsTrue(seen.Add(fd.Name), $"Repeated use of {fd.Name} at index {fd.StartIndex} in {modPath}");
                } else if (stmt is AssignmentStatement assign && assign.Left.FirstOrDefault() is NameExpression n) {
                    Assert.IsTrue(seen.Add(n.Name), $"Repeated use of {n.Name} at index {n.StartIndex} in {modPath}");
                }
            }

            // Ensure we can get all the builtin types
            foreach (BuiltinTypeId v in Enum.GetValues(typeof(BuiltinTypeId))) {
                var type = interpreter.GetBuiltinType(v);
                type.Should().NotBeNull().And.BeAssignableTo<IPythonType>($"Did not find {v}");
                type.IsBuiltin.Should().BeTrue();
            }

            // Ensure we cannot see or get builtin types directly
            mod.GetMemberNames().Should().NotContain(Enum.GetNames(typeof(BuiltinTypeId)).Select(n => $"__{n}"));

            foreach (var id in Enum.GetNames(typeof(BuiltinTypeId))) {
                mod.GetMember($"__{id}").Should().BeNull(id);
            }
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV38() {
            var v = PythonVersions.Python38 ?? PythonVersions.Python38_x64;
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV37() {
            var v = PythonVersions.Python37 ?? PythonVersions.Python37_x64;
            await FullStdLibTest(v);
        }


        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV36() {
            var v = PythonVersions.Python36 ?? PythonVersions.Python36_x64;
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(0)]
        public async Task FullStdLibV27() {
            var v = PythonVersions.Python27 ?? PythonVersions.Python27_x64;
            await FullStdLibTest(v);
        }

        [TestMethod, TestCategory("60s"), Priority(1)]
        [Timeout(10 * 60 * 1000)]
        public async Task FullStdLibAnaconda3() {
            var v = PythonVersions.Anaconda36_x64 ?? PythonVersions.Anaconda36;
            await FullStdLibTest(v,
                // Crashes Python on import
                @"sklearn.linear_model.cd_fast",
                // Crashes Python on import
                @"sklearn.cluster._k_means_elkan"
            );
        }

        [TestMethod, TestCategory("60s"), Priority(1)]
        [Timeout(10 * 60 * 1000)]
        public async Task FullStdLibAnaconda2() {
            var v = PythonVersions.Anaconda27_x64 ?? PythonVersions.Anaconda27;
            await FullStdLibTest(v,
                // Fails to import due to SxS manifest issues
                "dde",
                "win32ui"
            );
        }


        private async Task FullStdLibTest(InterpreterConfiguration configuration, params string[] skipModules) {
            configuration.AssertInstalled();
            var moduleUri = TestData.GetDefaultModuleUri();
            var moduleDirectory = Path.GetDirectoryName(moduleUri.LocalPath);

            var services = await CreateServicesAsync(moduleDirectory, configuration);
            var interpreter = services.GetService<IPythonInterpreter>();

            var modules = ModulePath.GetModulesInLib(configuration.LibraryPath, configuration.SitePackagesPath).ToList();

            var skip = new HashSet<string>(skipModules);
            skip.UnionWith(new[] {
                @"matplotlib.backends._backend_gdk",
                @"matplotlib.backends._backend_gtkagg",
                @"matplotlib.backends._gtkagg",
                "test.test_pep3131",
                "test.test_unicode_identifiers",
                "test.test_super" // nonlocal syntax error
            });
            skip.UnionWith(modules.Select(m => m.FullName)
                .Where(n => n.StartsWith(@"test.badsyntax") || n.StartsWith("test.bad_coding")));

            var anySuccess = false;
            var anyExtensionSuccess = false;
            var anyExtensionSeen = false;
            var anyParseError = false;

            foreach (var m in skip) {
                ((MainModuleResolution)interpreter.ModuleResolution).AddUnimportableModule(m);
            }

            var set = modules
                .Where(m => !skip.Contains(m.ModuleName))
                .GroupBy(m => {
                    var i = m.FullName.IndexOf('.');
                    return i <= 0 ? m.FullName : m.FullName.Remove(i);
                })
                .SelectMany(g => g.Select(m => Tuple.Create(m, m.ModuleName)))
                .ToArray();
            set = set.Where(x => x.Item2 != null && x.Item2.Contains("grammar")).ToArray();

            foreach (var r in set) {
                interpreter.ModuleResolution.GetOrLoadModule(r.Item2);
            }
            
            await services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            
            foreach (var r in set) {
                var modName = r.Item1;
                var mod = interpreter.ModuleResolution.GetOrLoadModule(r.Item2);

                anyExtensionSeen |= modName.IsNativeExtension;
                switch (mod) {
                    case null:
                        Trace.TraceWarning("failed to import {0} from {1}", modName.ModuleName, modName.SourceFile);
                        break;
                    case CompiledPythonModule compiledPythonModule:
                        var errors = compiledPythonModule.GetParseErrors().ToArray();
                        if (errors.Any()) {
                            anyParseError = true;
                            Trace.TraceError("Parse errors in {0}", modName.SourceFile);
                            foreach (var e in errors) {
                                Trace.TraceError(e.Message);
                            }
                        } else {
                            anySuccess = true;
                            anyExtensionSuccess |= modName.IsNativeExtension;
                        }

                        break;
                    case IPythonModule _: {
                            var filteredErrors = ((IDocument)mod).GetParseErrors().Where(e => !e.Message.Contains("encoding problem")).ToArray();
                            if (filteredErrors.Any()) {
                                // Do not fail due to errors in installed packages
                                if (!mod.FilePath.Contains("site-packages")) {
                                    anyParseError = true;
                                }
                                Trace.TraceError("Parse errors in {0}", modName.SourceFile);
                                foreach (var e in filteredErrors) {
                                    Trace.TraceError(e.Message);
                                }
                            } else {
                                anySuccess = true;
                                anyExtensionSuccess |= modName.IsNativeExtension;
                            }

                            break;
                        }
                }
            }
            Assert.IsTrue(anySuccess, "failed to import any modules at all");
            Assert.IsTrue(anyExtensionSuccess || !anyExtensionSeen, "failed to import all extension modules");
            Assert.IsFalse(anyParseError, "parse errors occurred");
        }
    }
}
