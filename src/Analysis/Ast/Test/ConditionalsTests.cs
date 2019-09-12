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

using System.Threading.Tasks;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ConditionalsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() { 
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task SysVersionInfoGreaterThan3(bool is3x) {
            const string code = @"
if sys.version_info >= (3, 0):
    x = 1
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            if (is3x) {
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            } else {
                analysis.Should().NotHaveVariable("x");
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task SysVersionInfoLessThan3(bool is3x) {
            const string code = @"
if sys.version_info < (3, 0):
    x = 1
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            if (!is3x) {
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            } else {
                analysis.Should().NotHaveVariable("x");
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task SysPlatformWindows(bool isWindows) {
            const string code = @"
if sys.platform == 'win32':
    x = 1
else:
    x = 'a'
";
            var platform = SubstitutePlatform(out var sm);
            platform.IsWindows.Returns(x => isWindows);
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X, sm);
            analysis.Should().HaveVariable("x").OfType(isWindows ? BuiltinTypeId.Int : BuiltinTypeId.Str);
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task SysPlatformNotWindows(bool isWindows) {
            const string code = @"
if sys.platform != 'win32':
    x = 1
";
            var platform = SubstitutePlatform(out var sm);
            platform.IsWindows.Returns(x => isWindows);
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X, sm);
            if (!isWindows) {
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            } else {
                analysis.Should().NotHaveVariable("x");
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task OsPathWindows(bool isWindows) {
            const string code = @"
if 'nt' in _names:
    x = 1
";
            var platform = SubstitutePlatform(out var sm);
            platform.IsWindows.Returns(x => isWindows);
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X, sm);
            if (isWindows) {
                analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int);
            } else {
                analysis.Should().NotHaveVariable("x");
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task OsPathPosix(bool isWindows) {
            const string code = @"
if 'posix' in _names:
    x = 1
else:
    x = 'a'
";
            var platform = SubstitutePlatform(out var sm);
            platform.IsWindows.Returns(x => isWindows);
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable2X, sm);
            analysis.Should().HaveVariable("x").OfType(isWindows ? BuiltinTypeId.Str : BuiltinTypeId.Int);
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task FunctionByVersion(bool is3x) {
            const string code = @"
if sys.version_info >= (3, 0):
   def func(a): ...
else:
   def func(a, b): ...
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            analysis.Should().HaveFunction("func")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameters(is3x ? new[] { "a" } : new[] { "a", "b" });
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod, Priority(0)]
        public async Task FunctionByVersionElif(bool is3x) {
            const string code = @"
if sys.version_info >= (3, 0):
   def func(a): ...
elif sys.version_info < (3, 0):
   def func(a, b): ...
";
            var analysis = await GetAnalysisAsync(code, is3x ? PythonVersions.LatestAvailable3X : PythonVersions.LatestAvailable2X);
            analysis.Should().HaveFunction("func")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveParameters(is3x ? new[] { "a" } : new[] { "a", "b" });
        }

        private IOSPlatform SubstitutePlatform(out IServiceManager sm) {
            sm = new ServiceManager();
            var platform = Substitute.For<IOSPlatform>();
            sm
                .AddService(TestLogger)
                .AddService(platform)
                .AddService(new ProcessServices())
                .AddService(new FileSystem(platform));
            return platform;
        }
    }
}
