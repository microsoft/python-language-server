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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class ValuesTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Values() {
            var code = await File.ReadAllTextAsync(Path.Combine(GetAnalysisTestDataFilesPath(), "Values.py"));
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("pi").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("l").OfType(BuiltinTypeId.List)
                .And.HaveVariable("t").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("d").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("s").OfType(BuiltinTypeId.Set)
                .And.HaveVariable("X").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("Y").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("Z").OfType(BuiltinTypeId.Bytes)
                .And.HaveVariable("PI").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("L").OfType(BuiltinTypeId.List)
                .And.HaveVariable("T").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("D").OfType(BuiltinTypeId.Dict)
                .And.HaveVariable("S").OfType(BuiltinTypeId.Set);
        }

        [TestMethod, Priority(0)]
        public async Task DocStrings() {
            var code = @"
def f():
    '''func doc'''


def funicode():
    u'''unicode func doc'''

class C:
    '''class doc'''

class CUnicode:
    u'''unicode class doc'''

class CNewStyle(object):
    '''new-style class doc'''

class CInherited(CNewStyle):
    pass

class CInit:
    def __init__(self):
        '''init doc'''
        pass

class CUnicodeInit:
    def __init__(self):
        u'''unicode init doc'''
        pass

class CNewStyleInit(object):
    '''new-style class doc'''
    def __init__(self):
        pass

class CInheritedInit(CNewStyleInit):
    pass
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveDocumentation("func doc");

            analysis.Should().HaveClass("C")
                .Which.Should().HaveDocumentation("class doc");

            analysis.Should().HaveFunction(@"funicode")
                .Which.Should().HaveSingleOverload()
                .Which.Should().HaveDocumentation("unicode func doc");

            analysis.Should().HaveClass("CUnicode")
                .Which.Should().HaveDocumentation("unicode class doc");

            analysis.Should().HaveClass("CNewStyle")
                .Which.Should().HaveDocumentation("new-style class doc");

            analysis.Should().HaveClass("CInherited")
                .Which.Should().HaveDocumentation("new-style class doc");

            analysis.Should().HaveClass("CInit")
                .Which.Should().HaveDocumentation("init doc");

            analysis.Should().HaveClass("CUnicodeInit")
                .Which.Should().HaveDocumentation("unicode init doc");

            analysis.Should().HaveClass("CNewStyleInit")
                .Which.Should().HaveDocumentation("new-style class doc");

            analysis.Should().HaveClass("CInheritedInit")
                .Which.Should().HaveDocumentation("new-style class doc");
        }

        [TestMethod, Priority(0)]
        public async Task WithStatement() {
            const string code = @"
class X(object):
    def x_method(self): pass
    def __enter__(self): return self
    def __exit__(self, exc_type, exc_value, traceback): return False
       
class Y(object):
    def y_method(self): pass
    def __enter__(self): return 123
    def __exit__(self, exc_type, exc_value, traceback): return False
 
with X() as x:
    pass #x

with Y() as y:
    pass #y
    
with X():
    pass
";
            var analysis = await GetAnalysisAsync(code);

            analysis.Should().HaveVariable("y").OfType(BuiltinTypeId.Int)
                    .And.HaveVariable("x")
                    .Which.Should().HaveMember("x_method");
        }

        [TestMethod, Priority(0)]
        public async Task Global() {
            const string code = @"
x = None
y = None
def f():
    def g():
        global x, y
        x = 123
        y = 123
    return x, y

a, b = f()
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("x").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("y").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task Nonlocal() {
            const string code = @"
def f():
    x = None
    y = None
    def g():
        nonlocal x, y
        x = 123
        y = 234
    return x, y

a, b = f()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("a").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("b").OfType(BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public async Task TryExcept() {
            const string code = @"
class MyException(Exception): pass

def f():
    try:
        pass
    except TypeError, e1:
        pass

def g():
    try:
        pass
    except MyException, e2:
        pass
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveFunction("f")
                .Which.Should().HaveVariable("e1").OfType("TypeError");

            analysis.Should().HaveFunction("g")
                .Which.Should().HaveVariable("e2").OfType("MyException");
        }
    }
}
