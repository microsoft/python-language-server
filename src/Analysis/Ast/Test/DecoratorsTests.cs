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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class DecoratorsTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            SharedServicesMode = true;
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        [Ignore]
        public async Task DecoratorClass() {
            const string code = @"
def dec1(C):
    def sub_method(self): pass
    C.sub_method = sub_method
    return C

@dec1
class MyBaseClass1(object):
    def base_method(self): pass

def dec2(C):
    class MySubClass(C):
        def sub_method(self): pass
    return MySubClass

@dec2
class MyBaseClass2(object):
    def base_method(self): pass

mc1 = MyBaseClass1()
mc2 = MyBaseClass2()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("mc1")
                .Which.Should().HaveMembers("base_method", "sub_method");
            analysis.Should().HaveVariable("mc2").OfType("MySubClass")
                .Which.Should().HaveMembers("sub_method");
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorReturnTypes_DecoratorNoParams() {
            const string code = @"# with decorator without wrap
def decoratorFunctionTakesArg1(f):
    def wrapped_f(arg):
        return f(arg)
    return wrapped_f

@decoratorFunctionTakesArg1
def returnsGivenWithDecorator1(parm):
    return parm

retGivenInt = returnsGivenWithDecorator1(1)
retGivenString = returnsGivenWithDecorator1('str')
retGivenBool = returnsGivenWithDecorator1(True)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("retGivenInt").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("retGivenString").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("retGivenBool").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorReturnTypes_DecoratorWithParams() {
            const string code = @"
# with decorator with wrap
def decoratorFunctionTakesArg2():
    def wrap(f):
        def wrapped_f(arg):
            return f(arg)
        return wrapped_f
    return wrap

@decoratorFunctionTakesArg2()
def returnsGivenWithDecorator2(parm):
    return parm

retGivenInt = returnsGivenWithDecorator2(1)
retGivenString = returnsGivenWithDecorator2('str')
retGivenBool = returnsGivenWithDecorator2(True)";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("retGivenInt").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("retGivenString").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("retGivenBool").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorReturnTypes_NoDecorator() {
            const string code = @"# without decorator
def returnsGiven(parm):
    return parm

retGivenInt = returnsGiven(1)
retGivenString = returnsGiven('str')
retGivenBool = returnsGiven(True)
";

            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("retGivenInt").OfType(BuiltinTypeId.Int)
                .And.HaveVariable("retGivenString").OfType(BuiltinTypeId.Str)
                .And.HaveVariable("retGivenBool").OfType(BuiltinTypeId.Bool);
        }

        [TestMethod, Priority(0)]
        [Ignore]
        public async Task DecoratorTypes() {
            var code = @"
def nop(fn):
    def wrap():
        return fn()
    wp = wrap
    return wp

@nop
def a_tuple():
    return (1, 2, 3)

@nop
def a_list():
    return [1, 2, 3]

@nop
def a_float():
    return 0.1

@nop
def a_string():
    return 'abc'

x = a_tuple()
y = a_list()
z = a_float()
w = a_string()
";
            var analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("x").OfType(BuiltinTypeId.Tuple)
                .And.HaveVariable("y").OfType(BuiltinTypeId.List)
                .And.HaveVariable("z").OfType(BuiltinTypeId.Float)
                .And.HaveVariable("w").OfType(BuiltinTypeId.Str);

            code = @"
def as_list(fn):
    def wrap(v):
        if v == 0:
            return list(fn())
        elif v == 1:
            return set(fn(*args, **kwargs))
        else:
            return str(fn())
    return wrap

@as_list
def items():
    return (1, 2, 3)

items2 = as_list(lambda: (1, 2, 3))

x = items(0)
";
            analysis = await GetAnalysisAsync(code);
            analysis.Should().HaveVariable("items").OfType(BuiltinTypeId.Function)
                .And.HaveVariable("items2").OfType(BuiltinTypeId.Function)
                .And.HaveVariable("x").OfType(BuiltinTypeId.List);
        }
    }
}
