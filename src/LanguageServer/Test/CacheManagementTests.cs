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
        public void DeleteAnalysisCache() {
            var sm = new ServiceManager();
            var fs = Substitute.For<IFileSystem>();
            sm.AddService(fs);

            var mdc = Substitute.For<IModuleDatabaseCache>();
            mdc.CacheFolder.Returns(c => "CacheFolder");
            sm.AddService(mdc);

            using (var s = new Implementation.Server(sm)) {
                s.ClearAnalysisCache();
                fs.Received().DeleteDirectory("CacheFolder", true);
            }
        }

        [TestMethod, Priority(0)]
        public async Task LsDeleteAnalysisCache() {
            var sm = new ServiceManager();
            var fs = Substitute.For<IFileSystem>();
            sm.AddService(fs);

            var mdc = Substitute.For<IModuleDatabaseCache>();
            mdc.CacheFolder.Returns(c => "CacheFolder");
            sm.AddService(mdc);

            using (var ls = new Implementation.LanguageServer())
            using (var ms = new MemoryStream()) {
                ls.Start(sm, new JsonRpc(ms));
                await ls.ClearAnalysisCache(CancellationToken.None);
                fs.Received().DeleteDirectory("CacheFolder", true);
            }
        }
    }
}
