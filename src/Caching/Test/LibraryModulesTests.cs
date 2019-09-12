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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class LibraryModulesTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private string BaselineFileName => GetBaselineFileName(TestContext.TestName);

        [TestMethod, Priority(0)]
        [Ignore("Builtins module have custom member handling. We do not persist it yet.")]
        public async Task Builtins() {
            var analysis = await GetAnalysisAsync(string.Empty);
            var builtins = analysis.Document.Interpreter.ModuleResolution.BuiltinsModule;
            var model = ModuleModel.FromAnalysis(builtins.Analysis, Services, AnalysisCachingLevel.Library);

            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);

            var dbModule = new PythonDbModule(model, null, Services);
            dbModule.Should().HaveSameMembersAs(builtins);
        }

        [TestMethod, Priority(0)]
        public Task Ast() => TestModule("ast");

        [TestMethod, Priority(0)]
        public Task Asyncio() => TestModule("asyncio");

        [TestMethod, Priority(0)]
        public Task Base64() => TestModule("base64");

        [TestMethod, Priority(0)]
        public Task Bisect() => TestModule("bisect");

        [TestMethod, Priority(0)]
        public Task Calendar() => TestModule("calendar");

        [TestMethod, Priority(0)]
        public Task Collections() => TestModule("collections");

        [TestMethod, Priority(0)]
        public Task Concurrent() => TestModule("concurrent");

        [TestMethod, Priority(0)]
        public Task Crypt() => TestModule("crypt");

        [TestMethod, Priority(0)]
        public Task Csv() => TestModule("csv");

        [TestMethod, Priority(0)]
        public Task CTypes() => TestModule("ctypes");

        [TestMethod, Priority(0)]
        public Task Curses() => TestModule("curses");

        [TestMethod, Priority(0)]
        public Task Dataclasses() => TestModule("dataclasses");

        [TestMethod, Priority(0)]
        public Task Datetime() => TestModule("datetime");

        [TestMethod, Priority(0)]
        public Task Dbm() => TestModule("dbm");

        [TestMethod, Priority(0)]
        public Task Distutils() => TestModule("distutils");

        [TestMethod, Priority(0)]
        public Task Email() => TestModule("email");

        [TestMethod, Priority(0)]
        public Task Encodings() => TestModule("encodings");

        [TestMethod, Priority(0)]
        public Task Enum() => TestModule("enum");

        [TestMethod, Priority(0)]
        public Task Filecmp() => TestModule("filecmp");

        [TestMethod, Priority(0)]
        public Task Fileinput() => TestModule("fileinput");

        [TestMethod, Priority(0)]
        public Task Fractions() => TestModule("fractions");

        [TestMethod, Priority(0)]
        public Task Ftplib() => TestModule("ftplib");

        [TestMethod, Priority(0)]
        public Task Functools() => TestModule("functools");

        [TestMethod, Priority(0)]
        public Task Genericpath() => TestModule("genericpath");

        [TestMethod, Priority(0)]
        public Task Glob() => TestModule("glob");

        [TestMethod, Priority(0)]
        public Task Gzip() => TestModule("gzip");

        [TestMethod, Priority(0)]
        public Task Hashlib() => TestModule("hashlib");

        [TestMethod, Priority(0)]
        public Task Heapq() => TestModule("heapq");

        [TestMethod, Priority(0)]
        public Task Html() => TestModule("html");

        [TestMethod, Priority(0)]
        public Task Http() => TestModule("http");

        [TestMethod, Priority(0)]
        public Task Importlib() => TestModule("importlib");

        [TestMethod, Priority(0)]
        public Task Imaplib() => TestModule("imaplib");

        [TestMethod, Priority(0)]
        public Task Imghdr() => TestModule("imghdr");

        [TestMethod, Priority(0)]
        public Task Inspect() => TestModule("inspect");

        [TestMethod, Priority(0)]
        public Task Io() => TestModule("io");

        [TestMethod, Priority(0)]
        public Task Json() => TestModule("json");

        [TestMethod, Priority(0)]
        public Task Logging() => TestModule("logging");

        [TestMethod, Priority(0)]
        public Task Lzma() => TestModule("lzma");

        [TestMethod, Priority(0)]
        public Task Mailbox() => TestModule("mailbox");

        [TestMethod, Priority(0)]
        public Task Multiprocessing() => TestModule("multiprocessing");

        [TestMethod, Priority(0)]
        public Task Numpy() => TestModule("numpy");

        [TestMethod, Priority(0)]
        public Task Os() => TestModule("os");

        [TestMethod, Priority(0)]
        public Task Pickle() => TestModule("pickle");

        [TestMethod, Priority(0)]
        public Task Pipes() => TestModule("pipes");

        [TestMethod, Priority(0)]
        public Task Pkgutil() => TestModule("pkgutil");

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/microsoft/python-language-server/issues/1434")]
        public Task Plistlib() => TestModule("plistlib");

        [TestMethod, Priority(0)]
        public Task Pstats() => TestModule("pstats");

        [TestMethod, Priority(0)]
        public Task Pydoc() => TestModule("pydoc");

        [TestMethod, Priority(0)]
        public Task Queue() => TestModule("queue");

        [TestMethod, Priority(0)]
        public Task Random() => TestModule("random");

        [TestMethod, Priority(0)]
        public Task Re() => TestModule("re");

        [TestMethod, Priority(0)]
        public Task Reprlib() => TestModule("reprlib");

        [TestMethod, Priority(0)]
        public Task Signal() => TestModule("signal");

        [TestMethod, Priority(0)]
        public Task Site() => TestModule("site");

        [TestMethod, Priority(0)]
        public Task Socket() => TestModule("socket");

        [TestMethod, Priority(0)]
        public Task Sqlite3() => TestModule("sqlite3");

        [TestMethod, Priority(0)]
        public Task Statistics() => TestModule("statistics");

        [TestMethod, Priority(0)]
        public Task String() => TestModule("string");

        [TestMethod, Priority(0)]
        public Task Ssl() => TestModule("ssl");

        [TestMethod, Priority(0)]
        public Task Sys() => TestModule("sys");

        [TestMethod, Priority(0)]
        public Task Tensorflow() => TestModule("tensorflow");

        [TestMethod, Priority(0)]
        public Task Time() => TestModule("time");

        [TestMethod, Priority(0)]
        public Task Threading() => TestModule("threading");

        [TestMethod, Priority(0)]
        public Task Tkinter() => TestModule("tkinter");

        [TestMethod, Priority(0)]
        public Task Token() => TestModule("token");

        [TestMethod, Priority(0)]
        public Task Trace() => TestModule("trace");

        [TestMethod, Priority(0)]
        public Task Types() => TestModule("types");

        [TestMethod, Priority(0)]
        public Task Unittest() => TestModule("unittest");

        [TestMethod, Priority(0)]
        public Task Urllib() => TestModule("urllib");

        [TestMethod, Priority(0)]
        public Task Uuid() => TestModule("uuid");

        [TestMethod, Priority(0)]
        public Task Weakref() => TestModule("weakref");

        [TestMethod, Priority(0)]
        public Task Xml() => TestModule("xml");

        [TestMethod, Priority(0)]
        public Task XmlRpc() => TestModule("xmlrpc");

        [TestMethod, Priority(0)]
        public Task Zipfile() => TestModule("zipfile");

        [TestMethod, Priority(0)]
        public async Task Requests() {
            const string code = @"
import requests
x = requests.get('microsoft.com')
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var v = analysis.GlobalScope.Variables["requests"];
            v.Should().NotBeNull();
            if (v.Value.GetPythonType<IPythonModule>().ModuleType == ModuleType.Unresolved) {
                Assert.Inconclusive("'requests' package is not installed.");
            }

            var rq = analysis.Document.Interpreter.ModuleResolution.GetImportedModule("requests");
            var model = ModuleModel.FromAnalysis(rq.Analysis, Services, AnalysisCachingLevel.Library);

            var u = model.UniqueId;
            u.Should().Contain("(").And.EndWith(")");
            var open = u.IndexOf('(');
            // Verify this looks like a version.
            new Version(u.Substring(open + 1, u.IndexOf(')') - open - 1));

            await CompareBaselineAndRestoreAsync(model, rq);
        }

        private async Task TestModule(string name) {
            var analysis = await GetAnalysisAsync($"import {name}", PythonVersions.Python36_x64);
            
            var m = analysis.Document.Interpreter.ModuleResolution.GetImportedModule(name);
            if (m == null || m.ModuleType == ModuleType.Unresolved) {
                Assert.Inconclusive($"Module {name} is not installed or otherwise could not be imported.");
                return;
            }

            var model = ModuleModel.FromAnalysis(m.Analysis, Services, AnalysisCachingLevel.Library);
            model.Should().NotBeNull($"Module {name} is either not installed or cannot be cached");

            await CompareBaselineAndRestoreAsync(model, m);
        }
    }
}
