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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using StreamJsonRpc;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class CacheManagementTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public Task DeleteAnalysisCache()
            => RunTest(sm => {
                using (var s = new Implementation.Server(sm)) {
                    s.ClearAnalysisCache();
                    return Task.CompletedTask;
                }
            });

        [TestMethod, Priority(0)]
        public Task LsDeleteAnalysisCache()
            => RunTest(sm => {
                using (var ls = new Implementation.LanguageServer())
                using (var ms = new MemoryStream()) {
                    ls.Start(sm, new JsonRpc(ms));
                    return ls.ClearAnalysisCache(CancellationToken.None);
                }
            });

        public async Task RunTest(Func<ServiceManager, Task> test) {
            using (var sm = new ServiceManager()) {
                var fs = Substitute.For<IFileSystem>();
                sm.AddService(fs);

                const string baseName = "analysis.v";
                SetupDirectories(fs, new[] { $"{baseName}1", $"{baseName}3" });

                var mdc = Substitute.For<IModuleDatabaseCache>();
                mdc.CacheFolder.Returns(c => "CacheFolder");
                sm.AddService(mdc);

                await test(sm);
                fs.Received().DeleteDirectory($"{baseName}1", true);
                fs.Received().DeleteDirectory($"{baseName}3", true);
            }
        }
        private static void SetupDirectories(IFileSystem fs, string[] names) {
            fs.GetFileSystemEntries(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
                .ReturnsForAnyArgs(c => names);
            fs.GetFileAttributes(Arg.Any<string>())
                .ReturnsForAnyArgs(c => FileAttributes.Directory);
        }
    }
}
