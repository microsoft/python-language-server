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

using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Tests;
using Microsoft.Python.Analysis.Types;
using Newtonsoft.Json;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    public abstract class AnalysisCachingTestBase: AnalysisTestBase {
        protected AnalysisCachingTestBase() {
            ModuleFactory.EnableMissingMemberAssertions = true;
        }

        protected static string ToJson(object model) {
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            using (var jw = new JsonTextWriter(sw)) {
                jw.Formatting = Formatting.Indented;
                var js = new JsonSerializer();
                js.Serialize(jw, model);
            }
            return sb.ToString();
        }

        protected string BaselineFilesFolder {
            get {
                var testAssembly = Assembly.GetExecutingAssembly().GetAssemblyPath();
                var outDirectory = Path.GetDirectoryName(testAssembly);
                return Path.GetFullPath(Path.Combine(outDirectory, "..", "..", "..", "src", "Caching", "Test", "Files"));
            }
        }

        protected string GetBaselineFileName(string testName, string suffix = null) 
            => Path.ChangeExtension(suffix == null 
                ? Path.Combine(BaselineFilesFolder, testName)
                : Path.Combine(BaselineFilesFolder, testName + suffix), "json");

        internal PythonDbModule CreateDbModule(ModuleModel model, string modulePath) {
            var dbModule = new PythonDbModule(model, modulePath, Services);
            dbModule.Construct(model);
            return dbModule;
        }

        internal async Task CompareBaselineAndRestoreAsync(ModuleModel model, IPythonModule m) {
            //var json = ToJson(model);
            //Baseline.CompareToFile(BaselineFileName, json);

            // In real case dependency analysis will restore model dependencies.
            // Here we don't go through the dependency analysis so we have to
            // manually restore dependent modules.
            foreach (var imp in model.Imports) {
                foreach (var name in imp.ModuleNames) {
                    m.Interpreter.ModuleResolution.GetOrLoadModule(name);
                }
            }
            foreach (var imp in model.FromImports) {
                foreach (var name in imp.RootNames) {
                    m.Interpreter.ModuleResolution.GetOrLoadModule(name);
                }
            }

            foreach (var imp in model.StubImports) {
                foreach (var name in imp.ModuleNames) {
                    m.Interpreter.TypeshedResolution.GetOrLoadModule(name);
                }
            }
            foreach (var imp in model.StubFromImports) {
                foreach (var name in imp.RootNames) {
                    m.Interpreter.TypeshedResolution.GetOrLoadModule(name);
                }
            }

            var analyzer = Services.GetService<IPythonAnalyzer>();
            await analyzer.WaitForCompleteAnalysisAsync();

            using (var dbModule = CreateDbModule(model, m.FilePath)) {
                dbModule.Should().HaveSameMembersAs(m);
            }
        }
    }
}
