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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Caching.Tests.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Caching.Tests {
    [TestClass]
    public class ClassesTests : AnalysisCachingTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        private string BaselineFileName => GetBaselineFileName(TestContext.TestName);

        [TestMethod, Priority(0)]
        public async Task NestedClasses() {
            const string code = @"
x = 'str'

class A:
    def methodA(self):
        return True

class B:
    x: int

    class C:
        def __init__(self):
            self.y = 1
        def methodC(self):
            return False
        
    def methodB1(self):
        return self.C()

    def methodB2(self):
        return self.C().y

c = B().methodB1()
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);
            var json = ToJson(model);
            Baseline.CompareToFile(BaselineFileName, json);
        }

        [TestMethod, Priority(0)]
        public async Task ForwardDeclarations() {
            const string code = @"
x = 'str'

class A:
    def methodA1(self):
        return B()

    def methodA2(self):
        return func()

class B:
    class C:
        def methodC(self):
            return func()
        
    def methodB1(self):
        def a():
            return 1
        return a

def func():
    return 1

a = B().methodB1()
b = A().methodA1()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").Which.Should().HaveType("a");
            analysis.Should().HaveVariable("b").Which.Should().HaveType("B");

            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);
            //var json = ToJson(model);
            //Baseline.CompareToFile(BaselineFileName, json);

            using (var dbModule = CreateDbModule(model, analysis.Document.FilePath)) {
                dbModule.Should().HaveSameMembersAs(analysis.Document);
            }
        }

        [TestMethod, Priority(0)]
        public async Task GenericClass() {
            const string code = @"
from typing import Generic, TypeVar, Dict

K = TypeVar('K')
V = TypeVar('V')

class A(Generic[K, V], Dict[K, V]):
    def key(self) -> K:
        return K

    def value(self):
        return V

x = A(1, 'a')
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);
            //var json = ToJson(model);
            //Baseline.CompareToFile(BaselineFileName, json);

            using (var dbModule = CreateDbModule(model, analysis.Document.FilePath)) {
                dbModule.Should().HaveSameMembersAs(analysis.Document);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassOwnDocumentation() {
            const string code = @"
class A:
    '''class A doc'''

class B(A):
    def __init__(self):
    '''__init__ doc'''
        return
";
            var analysis = await GetAnalysisAsync(code);
            var model = ModuleModel.FromAnalysis(analysis, Services, AnalysisCachingLevel.Library);
            var json = ToJson(model);
            // In JSON, class A should have 'class A doc' documentation while B should have none.
            Baseline.CompareToFile(BaselineFileName, json);
        }

         [TestMethod, Priority(0)]
        public void ClassesWithSuper() {
//            const string code = @"
//class A:
//    def methodA(self):
//        return True

//class B(A):
//    def methodB(self);
//        x = super()
//        return True
//";
            Assert.Inconclusive("Todo: super() persistence support");
        }

    }
}
