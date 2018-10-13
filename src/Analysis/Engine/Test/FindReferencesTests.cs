// Python Tools for Visual Studio
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class FindReferencesTests : ServerBasedTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        [TestMethod, Priority(0)]
        public async Task ImportStarCorrectRefs() {
            var text1 = @"
from mod2 import *

a = D()
";
            var text2 = @"
class D(object):
    pass
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri1 = TestData.GetTempPathUri("mod1.py");
                var uri2 = TestData.GetTempPathUri("mod2.py");

                await server.LoadFileAsync(uri1);
                await server.LoadFileAsync(uri2);

                await server.SendDidOpenTextDocument(uri1, text1);
                await server.SendDidOpenTextDocument(uri2, text2);

                var references = await server.SendFindReferences(uri2, 1, 7);
                references.Should().OnlyHaveReferences(
                    (uri1, (3, 4, 3, 5), ReferenceKind.Reference),
                    (uri2, (1, 0, 2, 8), ReferenceKind.Value),
                    (uri2, (1, 6, 1, 7), ReferenceKind.Definition)
                );
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/47")]
        public async Task MutatingReferences() {
            var text1 = @"
import mod2

class C(object):
    def SomeMethod(self):
        pass

mod2.D(C())
";

            var text2 = @"
class D(object):
    def __init__(self, value):
        self.value = value
        self.value.SomeMethod()
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri1 = TestData.GetNextModuleUri();
                var uri2 = TestData.GetNextModuleUri();
                await server.SendDidOpenTextDocument(uri1, text1);
                await server.SendDidOpenTextDocument(uri2, text2);

                var references = await server.SendFindReferences(uri1, 4, 9);

                references.Should().OnlyHaveReferences(
                    (uri1, (4, 4, 5, 12), ReferenceKind.Value),
                    (uri1, (4, 8, 4, 18), ReferenceKind.Definition),
                    (uri2, (4, 19, 4, 29), ReferenceKind.Reference)
                );

                text1 = text1.Substring(0, text1.IndexOf("    def")) + Environment.NewLine + text1.Substring(text1.IndexOf("    def"));
                await server.SendDidChangeTextDocumentAsync(uri1, text1);

                references = await server.SendFindReferences(uri1, 5, 9);
                references.Should().OnlyHaveReferences(
                    (uri1, (5, 4, 6, 12), ReferenceKind.Value),
                    (uri1, (5, 8, 5, 18), ReferenceKind.Definition),
                    (uri2, (4, 19, 4, 29), ReferenceKind.Reference)
                );

                text2 = Environment.NewLine + text2;
                await server.SendDidChangeTextDocumentAsync(uri2, text2);

                references = await server.SendFindReferences(uri1, 5, 9);
                references.Should().OnlyHaveReferences(
                    (uri1, (5, 4, 6, 12), ReferenceKind.Value),
                    (uri1, (5, 8, 5, 18), ReferenceKind.Definition),
                    (uri2, (5, 19, 5, 29), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ListComprehensions() {
            //            var entry = ProcessText(@"
            //x = [2,3,4]
            //y = [a for a in x]
            //z = y[0]
            //            ");

            //            AssertUtil.ContainsExactly(entry.GetTypesFromName("z", 0), IntType);

            string text = @"
def f(abc):
    print abc

[f(x) for x in [2,3,4]]
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 4, 2);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 2, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ExecReferences() {
            string text = @"
a = {}
b = """"
exec b in a
";
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.SendDidOpenTextDocument(uri, text);

                var referencesA = await server.SendFindReferences(uri, 1, 1);
                var referencesB = await server.SendFindReferences(uri, 2, 1);

                referencesA.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 1), ReferenceKind.Definition),
                    (uri, (3, 10, 3, 11), ReferenceKind.Reference)
                );

                referencesB.Should().OnlyHaveReferences(
                    (uri, (2, 0, 2, 1), ReferenceKind.Definition),
                    (uri, (3, 5, 3, 6), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task PrivateMemberReferences() {
            var text = @"
class C:
    def __x(self):
        pass

    def y(self):
        self.__x()

    def g(self):
        self._C__x()
";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                var references = await server.SendFindReferences(uri, 6, 14);

                references.Should().OnlyHaveReferences(
                    (uri, (2, 4, 3, 12), ReferenceKind.Value),
                    (uri, (2, 8, 2, 11), ReferenceKind.Definition),
                    (uri, (6, 13, 6, 16), ReferenceKind.Reference),
                    (uri, (9, 13, 9, 18), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task GeneratorComprehensions() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var text = @"
x = [2,3,4]
y = (a for a in x)
for z in y:
    print z
";

                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("z").OfResolvedType(BuiltinTypeId.Int);

                text = @"
x = [2,3,4]
y = (a for a in x)

def f(iterable):
    for z in iterable:
        print z

f(y)
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveVariable("z").OfResolvedType(BuiltinTypeId.Int);

                text = @"
x = [True, False, None]

def f(iterable):
    result = None
    for i in iterable:
        if i:
            result = i
    return result

y = f(i for i in x)
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("y").OfTypes(BuiltinTypeId.Bool, BuiltinTypeId.NoneType);

                text = @"
def f(abc):
    print abc

(f(x) for x in [2,3,4])
";
                analysis = await server.ChangeDefaultDocumentAndGetAnalysisAsync(text);
                var uri = TestData.GetDefaultModuleUri();
                var references = await server.SendFindReferences(uri, 1, 5);

                analysis.Should().HaveFunction("f")
                    .Which.Should().HaveParameter("abc").OfType(BuiltinTypeId.Int);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 2, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );

            }
        }

        /// <summary>
        /// Verifies that a line in triple quoted string which ends with a \ (eating the newline) doesn't throw
        /// off our newline tracking.
        /// </summary>
        [TestMethod, Priority(0)]
        public async Task ReferencesTripleQuotedStringWithBackslash() {
            // instance variables
            var text = @"
'''this is a triple quoted string\
that ends with a backslash on a line\
and our line info should remain correct'''

# add ref w/o type info
class C(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

";

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                await server.SendDidOpenTextDocument(uri, text);

                var referencesAbc = await server.SendFindReferences(uri, 8, 15);
                var referencesFob = await server.SendFindReferences(uri, 8, 21);

                referencesAbc.Should().OnlyHaveReferences(
                    (uri, (8, 13, 8, 16), ReferenceKind.Definition),
                    (uri, (9, 17, 9, 20), ReferenceKind.Reference),
                    (uri, (10, 19, 10, 22), ReferenceKind.Reference)
                );
                referencesFob.Should().OnlyHaveReferences(
                    (uri, (7, 23, 7, 26), ReferenceKind.Definition),
                    (uri, (8, 19, 8, 22), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task InstanceVariables() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();

                var text = @"
# add ref w/o type info
class C(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

";

                await server.SendDidOpenTextDocument(uri, text);

                var referencesAbc = await server.SendFindReferences(uri, 4, 15);
                var referencesFob = await server.SendFindReferences(uri, 4, 21);

                referencesAbc.Should().OnlyHaveReferences(
                    (uri, (4, 13, 4, 16), ReferenceKind.Definition),
                    (uri, (5, 17, 5, 20), ReferenceKind.Reference),
                    (uri, (6, 19, 6, 22), ReferenceKind.Reference)
                );
                referencesFob.Should().OnlyHaveReferences(
                    (uri, (3, 23, 3, 26), ReferenceKind.Definition),
                    (uri, (4, 19, 4, 22), ReferenceKind.Reference)
                );

                text = @"
# add ref w/ type info
class D(object):
    def __init__(self, fob):
        self.abc = fob
        del self.abc
        print self.abc

D(42)";
                await server.SendDidChangeTextDocumentAsync(uri, text);

                referencesAbc = await server.SendFindReferences(uri, 4, 15);
                referencesFob = await server.SendFindReferences(uri, 4, 21);
                var referencesD = await server.SendFindReferences(uri, 8, 1);

                referencesAbc.Should().OnlyHaveReferences(
                    (uri, (4, 13, 4, 16), ReferenceKind.Definition),
                    (uri, (5, 17, 5, 20), ReferenceKind.Reference),
                    (uri, (6, 19, 6, 22), ReferenceKind.Reference)
                );
                referencesFob.Should().OnlyHaveReferences(
                    (uri, (3, 23, 3, 26), ReferenceKind.Definition),
                    (uri, (4, 19, 4, 22), ReferenceKind.Reference)
                );
                referencesD.Should().OnlyHaveReferences(
                    (uri, (2, 6, 2, 7), ReferenceKind.Definition),
                    (uri, (2, 0, 6, 22), ReferenceKind.Value),
                    (uri, (8, 0, 8, 1), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctionDefinitions() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(): pass

x = f()";
                await server.SendDidOpenTextDocument(uri, text);

                var referencesF = await server.SendFindReferences(uri, 3, 5);
                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );

                text = @"
def f(): pass

x = f";
                await server.SendDidChangeTextDocumentAsync(uri, text);
                referencesF = await server.SendFindReferences(uri, 3, 5);

                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 13), ReferenceKind.Value),
                    (uri, (1, 4, 1, 5), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassVariables() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"

class D(object):
    abc = 42
    print abc
    del abc
";
                await server.SendDidOpenTextDocument(uri, text);
                var references = await server.SendFindReferences(uri, 3, 5);

                references.Should().OnlyHaveReferences(
                    (uri, (3, 4, 3, 7), ReferenceKind.Definition),
                    (uri, (4, 10, 4, 13), ReferenceKind.Reference),
                    (uri, (5, 8, 5, 11), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassDefinition() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
class D(object): pass

a = D
";
                await server.SendDidOpenTextDocument(uri, text);
                var references = await server.SendFindReferences(uri, 3, 5);

                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 21), ReferenceKind.Value),
                    (uri, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task MethodDefinition() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
class D(object): 
    def f(self): pass

a = D().f()
";
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 4, 9);
                references.Should().OnlyHaveReferences(
                    (uri, (2, 4, 2, 21), ReferenceKind.Value),
                    (uri, (2, 8, 2, 9), ReferenceKind.Definition),
                    (uri, (4, 8, 4, 9), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task Globals() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
abc = 42
print abc
del abc
";
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 2, 7);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 3), ReferenceKind.Definition),
                    (uri, (2, 6, 2, 9), ReferenceKind.Reference),
                    (uri, (3, 4, 3, 7), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task Parameters() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(abc):
    print abc
    abc = 42
    del abc
";
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 2, 11);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 7), ReferenceKind.Reference),
                    (uri, (2, 10, 2, 13), ReferenceKind.Reference),
                    (uri, (4, 8, 4, 11), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task NamedArguments() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(abc):
    print abc

f(abc = 123)
";
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 2, 11);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),
                    (uri, (2, 10, 2, 13), ReferenceKind.Reference),
                    (uri, (4, 2, 4, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task GrammarTest_Statements() {
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var uri = TestData.GetDefaultModuleUri();
                var text = @"
def f(abc):
    try: pass
    except abc: pass

    try: pass
    except TypeError, abc: pass

    abc, oar = 42, 23
    abc[23] = 42
    abc.fob = 42
    abc += 2

    class D(abc): pass

    for x in abc: print x

    import abc
    from xyz import abc
    from xyz import oar as abc

    if abc: print 'hi'
    elif abc: print 'bye'
    else: abc

    with abc:
        return abc

    print abc
    assert abc, abc

    raise abc
    raise abc, abc, abc

    while abc:
        abc
    else:
        abc

    for x in fob: 
        print x
    else:
        print abc

    try: pass
    except TypeError: pass
    else:
        abc
";
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 3, 12);

                // External module 'abc', URI varies depending on install
                var externalUri = references[1].uri;
                externalUri.LocalPath.Should().EndWith("abc.py");

                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),
                    (externalUri, (0, 0, 0, 0), ReferenceKind.Definition),

                    (uri, (3, 11, 3, 14), ReferenceKind.Reference),
                    (uri, (6, 22, 6, 25), ReferenceKind.Reference),

                    (uri, (8, 4, 8, 7), ReferenceKind.Reference),
                    (uri, (9, 4, 9, 7), ReferenceKind.Reference),
                    (uri, (10, 4, 10, 7), ReferenceKind.Reference),
                    (uri, (11, 4, 11, 7), ReferenceKind.Reference),

                    (uri, (13, 12, 13, 15), ReferenceKind.Reference),

                    (uri, (15, 13, 15, 16), ReferenceKind.Reference),

                    (uri, (17, 11, 17, 14), ReferenceKind.Reference),
                    (uri, (18, 20, 18, 23), ReferenceKind.Reference),
                    (uri, (19, 27, 19, 30), ReferenceKind.Reference),

                    (uri, (21, 7, 21, 10), ReferenceKind.Reference),
                    (uri, (22, 9, 22, 12), ReferenceKind.Reference),
                    (uri, (23, 10, 23, 13), ReferenceKind.Reference),

                    (uri, (25, 9, 25, 12), ReferenceKind.Reference),
                    (uri, (26, 15, 26, 18), ReferenceKind.Reference),

                    (uri, (28, 10, 28, 13), ReferenceKind.Reference),
                    (uri, (29, 11, 29, 14), ReferenceKind.Reference),
                    (uri, (29, 16, 29, 19), ReferenceKind.Reference),

                    (uri, (31, 10, 31, 13), ReferenceKind.Reference),
                    (uri, (32, 15, 32, 18), ReferenceKind.Reference),
                    (uri, (32, 20, 32, 23), ReferenceKind.Reference),
                    (uri, (32, 10, 32, 13), ReferenceKind.Reference),

                    (uri, (34, 10, 34, 13), ReferenceKind.Reference),
                    (uri, (35, 8, 35, 11), ReferenceKind.Reference),
                    (uri, (37, 8, 37, 11), ReferenceKind.Reference),

                    (uri, (42, 14, 42, 17), ReferenceKind.Reference),

                    (uri, (47, 8, 47, 11), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task GrammarTest_Expressions() {
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var text = @"
def f(abc):
    x = abc + 2
    x = 2 + abc
    x = l[abc]
    x = abc[l]
    x = abc.fob
    
    g(abc)

    abc if abc else abc

    {abc:abc},
    [abc, abc]
    (abc, abc)
    {abc}

    yield abc
    [x for x in abc]
    (x for x in abc)

    abc or abc
    abc and abc

    +abc
    x[abc:abc:abc]

    abc == abc
    not abc

    lambda : abc
";
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 3, 12);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 9), ReferenceKind.Definition),

                    (uri, (2, 8, 2, 11), ReferenceKind.Reference),
                    (uri, (3, 12, 3, 15), ReferenceKind.Reference),

                    (uri, (4, 10, 4, 13), ReferenceKind.Reference),
                    (uri, (5, 8, 5, 11), ReferenceKind.Reference),
                    (uri, (6, 8, 6, 11), ReferenceKind.Reference),
                    (uri, (8, 6, 8, 9), ReferenceKind.Reference),

                    (uri, (10, 11, 10, 14), ReferenceKind.Reference),
                    (uri, (10, 4, 10, 7), ReferenceKind.Reference),
                    (uri, (10, 20, 10, 23), ReferenceKind.Reference),

                    (uri, (12, 5, 12, 8), ReferenceKind.Reference),
                    (uri, (12, 9, 12, 12), ReferenceKind.Reference),
                    (uri, (13, 5, 13, 8), ReferenceKind.Reference),
                    (uri, (13, 10, 13, 13), ReferenceKind.Reference),
                    (uri, (14, 5, 14, 8), ReferenceKind.Reference),
                    (uri, (14, 10, 14, 13), ReferenceKind.Reference),
                    (uri, (15, 5, 15, 8), ReferenceKind.Reference),

                    (uri, (17, 10, 17, 13), ReferenceKind.Reference),
                    (uri, (18, 16, 18, 19), ReferenceKind.Reference),
                    (uri, (21, 4, 21, 7), ReferenceKind.Reference),

                    (uri, (21, 11, 21, 14), ReferenceKind.Reference),
                    (uri, (22, 4, 22, 7), ReferenceKind.Reference),
                    (uri, (22, 12, 22, 15), ReferenceKind.Reference),
                    (uri, (24, 5, 24, 8), ReferenceKind.Reference),

                    (uri, (25, 6, 25, 9), ReferenceKind.Reference),
                    (uri, (25, 10, 25, 13), ReferenceKind.Reference),
                    (uri, (25, 14, 25, 17), ReferenceKind.Reference),
                    (uri, (27, 4, 27, 7), ReferenceKind.Reference),

                    (uri, (27, 11, 27, 14), ReferenceKind.Reference),
                    (uri, (28, 8, 28, 11), ReferenceKind.Reference),
                    (uri, (19, 16, 19, 19), ReferenceKind.Reference),

                    (uri, (30, 13, 30, 16), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task Parameters_NestedFunction() {
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var text = @"
def f(a):
    def g():
        print(a)
        assert isinstance(a, int)
        a = 200
        print(a)
";
                await server.SendDidOpenTextDocument(uri, text);

                var expected = new (Uri documentUri, (int, int, int, int), ReferenceKind?)[] {
                    (uri, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uri, (3, 14, 3, 15), ReferenceKind.Reference),
                    (uri, (4, 26, 4, 27), ReferenceKind.Reference),
                    (uri, (5, 8, 5, 9), ReferenceKind.Reference),
                    (uri, (6, 14, 6, 15), ReferenceKind.Reference)
                };
                var references = await server.SendFindReferences(uri, 3, 15);
                references.Should().OnlyHaveReferences(expected);

                references = await server.SendFindReferences(uri, 5, 9);
                references.Should().OnlyHaveReferences(expected);

                references = await server.SendFindReferences(uri, 6, 15);
                references.Should().OnlyHaveReferences(expected);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassLocalVariable() {
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                var text = @"
a = 1
class B:
    a = 2
";
                await server.SendDidOpenTextDocument(uri, text);
                var references = await server.SendFindReferences(uri, 3, 5);
                references.Should().OnlyHaveReferences(
                    (uri, (3, 4, 3, 5), ReferenceKind.Definition)
                );
                references = await server.SendFindReferences(uri, 1, 1);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 0, 1, 1), ReferenceKind.Definition)
                );
            }
        }


        [TestMethod, Priority(0)]
        public async Task ListDictArgReferences() {
            var text = @"
def f(*a, **k):
    x = a[1]
    y = k['a']

#out
a = 1
k = 2
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(uri, text);

                var referencesA = await server.SendFindReferences(uri, 2, 8);
                var referencesK = await server.SendFindReferences(uri, 3, 8);
                referencesA.Should().OnlyHaveReferences(
                    (uri, (1, 7, 1, 8), ReferenceKind.Definition),
                    (uri, (2, 8, 2, 9), ReferenceKind.Reference)
                );
                referencesK.Should().OnlyHaveReferences(
                    (uri, (1, 12, 1, 13), ReferenceKind.Definition),
                    (uri, (3, 8, 3, 9), ReferenceKind.Reference)
                );

                referencesA = await server.SendFindReferences(uri, 6, 1);
                referencesK = await server.SendFindReferences(uri, 7, 1);
                referencesA.Should().OnlyHaveReference(uri, (6, 0, 6, 1), ReferenceKind.Definition);
                referencesK.Should().OnlyHaveReference(uri, (7, 0, 7, 1), ReferenceKind.Definition);
            }
        }

        [TestMethod, Priority(0)]
        public async Task KeywordArgReferences() {
            var text = @"
def f(a):
    pass

f(a=1)
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(uri, text);

                var references = await server.SendFindReferences(uri, 4, 3);
                references.Should().OnlyHaveReferences(
                    (uri, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uri, (4, 2, 4, 3), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReferencesCrossModule() {
            var fobText = @"
from oar import abc

abc()
";
            var oarText = "class abc(object): pass";
            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.LoadFileAsync(fobUri);
                await server.LoadFileAsync(oarUri);

                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);

                var references = await server.SendFindReferences(oarUri, 0, 7);
                references.Should().OnlyHaveReferences(
                    (oarUri, (0, 0, 0, 23), ReferenceKind.Value),
                    (oarUri, (0, 6, 0, 9), ReferenceKind.Definition),
                    (fobUri, (1, 16, 1, 19), ReferenceKind.Reference),
                    (fobUri, (3, 0, 3, 3), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task SuperclassMemberReferencesCrossModule() {
            // https://github.com/Microsoft/PTVS/issues/2271

            var fobText = @"
from oar import abc

class bcd(abc):
    def test(self):
        self.x
";
            var oarText = @"class abc(object):
    def __init__(self):
        self.x = 123
";

            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.LoadFileAsync(fobUri);
                await server.LoadFileAsync(oarUri);

                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);

                var references = await server.SendFindReferences(oarUri, 2, 14);
                references.Should().OnlyHaveReferences(
                    (oarUri, (2, 13, 2, 14), ReferenceKind.Definition),
                    (fobUri, (5, 13, 5, 14), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReferencesCrossMultiModule() {
            var fobText = @"
from oarbaz import abc

abc()
";
            var oarText = "class abc1(object): pass";
            var bazText = "\n\n\n\nclass abc2(object): pass";
            var oarBazText = @"from oar import abc1 as abc
from baz import abc2 as abc";

            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            var bazUri = TestData.GetTempPathUri("baz.py");
            var oarBazUri = TestData.GetTempPathUri("oarbaz.py");
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);
                await server.SendDidOpenTextDocument(bazUri, bazText);
                await server.SendDidOpenTextDocument(oarBazUri, oarBazText);

                var referencesAbc1 = await server.SendFindReferences(oarUri, 0, 7);
                var referencesAbc2 = await server.SendFindReferences(bazUri, 4, 7);
                var referencesAbc = await server.SendFindReferences(fobUri, 3, 1);

                referencesAbc1.Should().OnlyHaveReferences(
                    (oarUri, (0, 0, 0, 24), ReferenceKind.Value),
                    (oarUri, (0, 6, 0, 10), ReferenceKind.Definition),
                    (oarBazUri, (0, 16, 0, 20), ReferenceKind.Reference),
                    (oarBazUri, (0, 24, 0, 27), ReferenceKind.Reference)
                );

                referencesAbc2.Should().OnlyHaveReferences(
                    (bazUri, (4, 0, 4, 24), ReferenceKind.Value),
                    (bazUri, (4, 6, 4, 10), ReferenceKind.Definition),
                    (oarBazUri, (1, 16, 1, 20), ReferenceKind.Reference),
                    (oarBazUri, (1, 24, 1, 27), ReferenceKind.Reference)
                );

                referencesAbc.Should().OnlyHaveReferences(
                    (oarUri, (0, 0, 0, 24), ReferenceKind.Value),
                    (bazUri, (4, 0, 4, 24), ReferenceKind.Value),
                    (oarBazUri, (0, 16, 0, 20), ReferenceKind.Reference),
                    (oarBazUri, (0, 24, 0, 27), ReferenceKind.Reference),
                    (oarBazUri, (1, 16, 1, 20), ReferenceKind.Reference),
                    (oarBazUri, (1, 24, 1, 27), ReferenceKind.Reference),
                    (fobUri, (1, 19, 1, 22), ReferenceKind.Reference),
                    (fobUri, (3, 0, 3, 3), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportStarReferences() {
            var fobText = @"
CONSTANT = 1
class Class: pass
def fn(): pass";
            var oarText = @"from fob import *



x = CONSTANT
c = Class()
f = fn()";
            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);

                var referencesConstant = await server.SendFindReferences(oarUri, 4, 5);
                var referencesClass = await server.SendFindReferences(oarUri, 5, 5);
                var referencesfn = await server.SendFindReferences(oarUri, 6, 5);

                referencesConstant.Should().OnlyHaveReferences(
                    (fobUri, (1, 0, 1, 8), ReferenceKind.Definition),
                    (oarUri, (4, 4, 4, 12), ReferenceKind.Reference)
                );

                referencesClass.Should().OnlyHaveReferences(
                    (fobUri, (2, 0, 2, 17), ReferenceKind.Value),
                    (fobUri, (2, 6, 2, 11), ReferenceKind.Definition),
                    (oarUri, (5, 4, 5, 9), ReferenceKind.Reference)
                );

                referencesfn.Should().OnlyHaveReferences(
                    (fobUri, (3, 0, 3, 14), ReferenceKind.Value),
                    (fobUri, (3, 4, 3, 6), ReferenceKind.Definition),
                    (oarUri, (6, 4, 6, 6), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ImportAsReferences() {
            var fobText = @"
CONSTANT = 1
class Class: pass
def fn(): pass";
            var oarText = @"from fob import CONSTANT as CO, Class as Cl, fn as f



x = CO
c = Cl()
g = f()";

            var fobUri = TestData.GetTempPathUri("fob.py");
            var oarUri = TestData.GetTempPathUri("oar.py");
            using (var server = await CreateServerAsync()) {
                await server.SendDidOpenTextDocument(fobUri, fobText);
                await server.SendDidOpenTextDocument(oarUri, oarText);

                var referencesConstant = await server.SendFindReferences(fobUri, 1, 1);
                var referencesClass = await server.SendFindReferences(fobUri, 2, 7);
                var referencesFn = await server.SendFindReferences(fobUri, 3, 5);
                var referencesC0 = await server.SendFindReferences(oarUri, 4, 5);
                var referencesC1 = await server.SendFindReferences(oarUri, 5, 5);
                var referencesF = await server.SendFindReferences(oarUri, 6, 5);

                referencesConstant.Should().OnlyHaveReferences(
                    (fobUri, (1, 0, 1, 8), ReferenceKind.Definition),
                    (oarUri, (0, 16, 0, 24), ReferenceKind.Reference),
                    (oarUri, (0, 28, 0, 30), ReferenceKind.Reference),
                    (oarUri, (4, 4, 4, 6), ReferenceKind.Reference)
                );

                referencesClass.Should().OnlyHaveReferences(
                    (fobUri, (2, 0, 2, 17), ReferenceKind.Value),
                    (fobUri, (2, 6, 2, 11), ReferenceKind.Definition),
                    (oarUri, (0, 32, 0, 37), ReferenceKind.Reference),
                    (oarUri, (0, 41, 0, 43), ReferenceKind.Reference),
                    (oarUri, (5, 4, 5, 6), ReferenceKind.Reference)
                );

                referencesFn.Should().OnlyHaveReferences(
                    (fobUri, (3, 0, 3, 14), ReferenceKind.Value),
                    (fobUri, (3, 4, 3, 6), ReferenceKind.Definition),
                    (oarUri, (0, 45, 0, 47), ReferenceKind.Reference),
                    (oarUri, (0, 51, 0, 52), ReferenceKind.Reference),
                    (oarUri, (6, 4, 6, 5), ReferenceKind.Reference)
                );

                referencesC0.Should().OnlyHaveReferences(
                    (fobUri, (1, 0, 1, 8), ReferenceKind.Definition),
                    (oarUri, (0, 16, 0, 24), ReferenceKind.Reference),
                    (oarUri, (0, 28, 0, 30), ReferenceKind.Reference),
                    (oarUri, (4, 4, 4, 6), ReferenceKind.Reference)
                );

                referencesC1.Should().OnlyHaveReferences(
                    (fobUri, (2, 0, 2, 17), ReferenceKind.Value),
                    (fobUri, (2, 6, 2, 11), ReferenceKind.Definition),
                    (oarUri, (0, 32, 0, 37), ReferenceKind.Reference),
                    (oarUri, (0, 41, 0, 43), ReferenceKind.Reference),
                    (oarUri, (5, 4, 5, 6), ReferenceKind.Reference)
                );

                referencesF.Should().OnlyHaveReferences(
                    (fobUri, (3, 0, 3, 14), ReferenceKind.Value),
                    (fobUri, (3, 4, 3, 6), ReferenceKind.Definition),
                    (oarUri, (0, 45, 0, 47), ReferenceKind.Reference),
                    (oarUri, (0, 51, 0, 52), ReferenceKind.Reference),
                    (oarUri, (6, 4, 6, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReferencesGeneratorsV3() {
            var text = @"
[f for f in x]
[x for x in f]
(g for g in y)
(y for y in g)
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                await server.SendDidOpenTextDocument(uri, text);

                var referencesF = await server.SendFindReferences(uri, 1, 8);
                var referencesX = await server.SendFindReferences(uri, 2, 8);
                var referencesG = await server.SendFindReferences(uri, 3, 8);
                var referencesY = await server.SendFindReferences(uri, 4, 8);

                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 7, 1, 8), ReferenceKind.Definition),
                    (uri, (1, 1, 1, 2), ReferenceKind.Reference)
                );
                referencesX.Should().OnlyHaveReferences(
                    (uri, (2, 7, 2, 8), ReferenceKind.Definition),
                    (uri, (2, 1, 2, 2), ReferenceKind.Reference)
                );
                referencesG.Should().OnlyHaveReferences(
                    (uri, (3, 7, 3, 8), ReferenceKind.Definition),
                    (uri, (3, 1, 3, 2), ReferenceKind.Reference)
                );
                referencesY.Should().OnlyHaveReferences(
                    (uri, (4, 7, 4, 8), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task ReferencesGeneratorsV2() {
            var text = @"
[f for f in x]
[x for x in f]
(g for g in y)
(y for y in g)
";
            var uri = TestData.GetDefaultModuleUri();
            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable2X)) {
                await server.SendDidOpenTextDocument(uri, text);

                var referencesF = await server.SendFindReferences(uri, 1, 8);
                var referencesX = await server.SendFindReferences(uri, 2, 8);
                var referencesG = await server.SendFindReferences(uri, 3, 8);
                var referencesY = await server.SendFindReferences(uri, 4, 8);

                referencesF.Should().OnlyHaveReferences(
                    (uri, (1, 7, 1, 8), ReferenceKind.Definition),
                    (uri, (1, 1, 1, 2), ReferenceKind.Reference),
                    (uri, (2, 12, 2, 13), ReferenceKind.Reference)
                );
                referencesX.Should().OnlyHaveReferences(
                    (uri, (2, 7, 2, 8), ReferenceKind.Definition),
                    (uri, (2, 1, 2, 2), ReferenceKind.Reference)
                );
                referencesG.Should().OnlyHaveReferences(
                    (uri, (3, 7, 3, 8), ReferenceKind.Definition),
                    (uri, (3, 1, 3, 2), ReferenceKind.Reference)
                );
                referencesY.Should().OnlyHaveReferences(
                    (uri, (4, 7, 4, 8), ReferenceKind.Definition),
                    (uri, (4, 1, 4, 2), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctionScoping() {
            var code = @"x = 100

def f(x = x):
    x

f('abc')
";

            using (var server = await CreateServerAsync()) {
                var uri = TestData.GetDefaultModuleUri();
                await server.OpenDefaultDocumentAndGetAnalysisAsync(code);

                var referencesx1 = await server.SendFindReferences(uri, 2, 6);
                var referencesx2 = await server.SendFindReferences(uri, 2, 10);
                var referencesx3 = await server.SendFindReferences(uri, 3, 4);

                referencesx1.Should().OnlyHaveReferences(
                    (uri, (2, 6, 2, 7), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );

                referencesx2.Should().OnlyHaveReferences(
                    (uri, (0, 0, 0, 1), ReferenceKind.Definition),
                    (uri, (2, 10, 2, 11), ReferenceKind.Reference)
                );

                referencesx3.Should().OnlyHaveReferences(
                    (uri, (2, 6, 2, 7), ReferenceKind.Definition),
                    (uri, (3, 4, 3, 5), ReferenceKind.Reference)
                );
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/117")]
        public async Task MoveClass() {
            var fobSrc = "";

            var oarSrc = @"
class C(object):
    pass
";

            var bazSrc = @"
class C(object):
    pass
";
            using (var server = await CreateServerAsync()) {
                var uriFob = await server.OpenDocumentAndGetUriAsync("fob.py", fobSrc);
                var uriOar = await server.OpenDocumentAndGetUriAsync("oar.py", oarSrc);
                var uriBaz = await server.OpenDocumentAndGetUriAsync("baz.py", bazSrc);
                await server.SendDidChangeTextDocumentAsync(uriFob, "from oar import C");

                var references = await server.SendFindReferences(uriFob, 0, 17);
                references.Should().OnlyHaveReferences(
                    (uriFob, (0, 16, 0, 17), ReferenceKind.Reference),
                    (uriOar, (1, 0, 2, 8), ReferenceKind.Value),
                    (uriOar, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uriOar, (0, 0, 0, 0), ReferenceKind.Definition)
                );

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                var analysis = await server.GetAnalysisAsync(uriFob);
                analysis.Should().HaveVariable("C").WithDescription("C");

                // delete the class..
                await server.SendDidChangeTextDocumentAsync(uriOar, "");

                await server.WaitForCompleteAnalysisAsync(CancellationToken.None);
                analysis = await server.GetAnalysisAsync(uriFob);
                analysis.Should().HaveVariable("C").WithNoTypes();

                // Change location of the class
                await server.SendDidChangeTextDocumentAsync(uriFob, "from baz import C");

                references = await server.SendFindReferences(uriFob, 0, 17);
                references.Should().OnlyHaveReferences(
                    (uriFob, (0, 16, 0, 17), ReferenceKind.Reference),
                    (uriBaz, (1, 0, 2, 8), ReferenceKind.Value),
                    (uriBaz, (1, 6, 1, 7), ReferenceKind.Definition),
                    (uriBaz, (0, 0, 0, 0), ReferenceKind.Definition)
                );

                analysis = await server.GetAnalysisAsync(uriFob);
                analysis.Should().HaveVariable("C").WithDescription("C");
            }
        }

        [TestMethod, Priority(0)]
        public async Task DecoratorReferences() {
            var text = @"
def d1(f):
    return f
class d2:
    def __call__(self, f): return f

@d1
def func_d1(): pass
@d2()
def func_d2(): pass

@d1
class cls_d1(object): pass
@d2()
class cls_d2(object): pass
";
            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var referencesD1 = await server.SendFindReferences(uri, 1, 5);
                var referencesD2 = await server.SendFindReferences(uri, 3, 7);
                var analysis = await server.GetAnalysisAsync(uri);

                referencesD1.Should().OnlyHaveReferences(
                    (uri, (1, 0, 2, 12), ReferenceKind.Value),
                    (uri, (1, 4, 1, 6), ReferenceKind.Definition),
                    (uri, (6, 1, 6, 3), ReferenceKind.Reference),
                    (uri, (11, 1, 11, 3), ReferenceKind.Reference));

                referencesD2.Should().OnlyHaveReferences(
                    (uri, (3, 0, 4, 35), ReferenceKind.Value),
                    (uri, (3, 6, 3, 8), ReferenceKind.Definition),
                    (uri, (8, 1, 8, 3), ReferenceKind.Reference),
                    (uri, (13, 1, 13, 3), ReferenceKind.Reference));

                analysis.Should().HaveFunction("d1").WithParameter("f").OfTypes(BuiltinTypeId.Function, BuiltinTypeId.Type)
                    .And.HaveClass("d2").WithFunction("__call__").WithParameter("f").OfTypes(BuiltinTypeId.Function, BuiltinTypeId.Type)
                    .And.HaveFunction("func_d1")
                    .And.HaveFunction("func_d2")
                    .And.HaveClass("cls_d1")
                    .And.HaveClass("cls_d2");
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/215")]
        public async Task Nonlocal() {
            var text = @"
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

            using (var server = await CreateServerAsync(PythonVersions.LatestAvailable3X)) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var referencesX = await server.SendFindReferences(uri, 2, 5);
                var referencesY = await server.SendFindReferences(uri, 3, 5);
                var analysis = await server.GetAnalysisAsync(uri);

                analysis.Should().HaveVariable("a").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)
                    .And.HaveVariable("b").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)

                    .And.HaveFunction("f")
                    .Which.Should().HaveVariable("x").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)

                    .And.HaveFunction("g")
                    .Which.Should().HaveVariable("x").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int)
                    .And.HaveVariable("y").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int);

                referencesX.Should().OnlyHaveReferences(
                    (uri, (2, 4, 2, 5), ReferenceKind.Definition),
                    (uri, (5, 17, 5, 18), ReferenceKind.Reference),
                    (uri, (6, 8, 6, 9), ReferenceKind.Definition),
                    (uri, (8, 11, 8, 12), ReferenceKind.Reference));

                referencesY.Should().OnlyHaveReferences(
                    (uri, (3, 4, 3, 5), ReferenceKind.Definition),
                    (uri, (5, 20, 5, 21), ReferenceKind.Reference),
                    (uri, (7, 8, 7, 9), ReferenceKind.Definition),
                    (uri, (8, 14, 8, 15), ReferenceKind.Reference));

                text = @"
def f(x):
    def g():
        nonlocal x
        x = 123
    return x

a = f(None)
";

                await server.SendDidChangeTextDocumentAsync(uri, text);
                analysis = await server.GetAnalysisAsync(uri);
                analysis.Should().HaveVariable("a").OfTypes(BuiltinTypeId.NoneType, BuiltinTypeId.Int);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IsInstance() {
            var text = @"
def fob():
    oar = get_b()
    assert isinstance(oar, float)

    if oar.complex:
        raise IndexError

    return oar";

            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var references1 = await server.SendFindReferences(uri, 2, 5);
                var references2 = await server.SendFindReferences(uri, 3, 23);
                var references3 = await server.SendFindReferences(uri, 5, 8);
                var references4 = await server.SendFindReferences(uri, 8, 12);

                var expectations = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri, (2, 4, 2, 7), ReferenceKind.Definition),
                    (uri, (3, 22, 3, 25), ReferenceKind.Reference),
                    (uri, (5, 7, 5, 10), ReferenceKind.Reference),
                    (uri, (8, 11, 8, 14), ReferenceKind.Reference)
                };

                references1.Should().OnlyHaveReferences(expectations);
                references2.Should().OnlyHaveReferences(expectations);
                references3.Should().OnlyHaveReferences(expectations);
                references4.Should().OnlyHaveReferences(expectations);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FunctoolsDecorator() {
            var text = @"from functools import wraps

def d(f):
    @wraps(f)
    def wrapped(*a, **kw):
        return f(*a, **kw)
    return wrapped

@d
def g(p):
    return p

n1 = g(1)";

            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var referencesD = await server.SendFindReferences(uri, 2, 5);
                var referencesG = await server.SendFindReferences(uri, 9, 5);

                referencesD.Should().OnlyHaveReferences(
                    (uri, (2, 0, 6, 18), ReferenceKind.Value),
                    (uri, (2, 4, 2, 5), ReferenceKind.Definition),
                    (uri, (8, 1, 8, 2), ReferenceKind.Reference));
                referencesG.Should().OnlyHaveReferences(
                    (uri, (3, 4, 5, 26), ReferenceKind.Value),
                    (uri, (9, 4, 9, 5), ReferenceKind.Definition),
                    (uri, (12, 5, 12, 6), ReferenceKind.Reference));

                // Decorators that don't use @wraps will expose the wrapper function
                // as a value.
                text = @"def d(f):
    def wrapped(*a, **kw):
        return f(*a, **kw)
    return wrapped

@d
def g(p):
    return p

n1 = g(1)";

                await server.SendDidChangeTextDocumentAsync(uri, text);
                referencesD = await server.SendFindReferences(uri, 0, 5);
                referencesG = await server.SendFindReferences(uri, 6, 5);

                referencesD.Should().OnlyHaveReferences(
                    (uri, (0, 0, 3, 18), ReferenceKind.Value),
                    (uri, (0, 4, 0, 5), ReferenceKind.Definition),
                    (uri, (5, 1, 5, 2), ReferenceKind.Reference));
                referencesG.Should().OnlyHaveReferences(
                    (uri, (1, 4, 2, 26), ReferenceKind.Value),
                    (uri, (6, 4, 6, 5), ReferenceKind.Definition),
                    (uri, (9, 5, 9, 6), ReferenceKind.Reference));
            }
        }

        /// <summary>
        /// Variable is referred to in the base class, defined in the derived class, we should know the type information.
        /// </summary>
        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/229")]
        public async Task BaseReferencedDerivedDefined() {
            var text = @"
class Base(object):
    def f(self):
        x = self.map

class Derived(Base):
    def __init__(self):
        self.map = {}

pass

derived = Derived()
";

            using (var server = await CreateServerAsync()) {
                var analysis = await server.OpenDefaultDocumentAndGetAnalysisAsync(text);
                analysis.Should().HaveVariable("derived").WithValue<IInstanceInfo>().WithMemberOfType("map", PythonMemberType.Field);
            }
        }

        [TestMethod, Priority(0)]
        [Ignore("https://github.com/Microsoft/python-language-server/issues/218")]
        public async Task SubclassFindAllRefs() {
            var text = @"
class Base(object):
    def __init__(self):
        self.fob()

    def fob(self): 
        pass


class Derived(Base):
    def fob(self): 
        'x'
";

            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var references1 = await server.SendFindReferences(uri, 3, 14);
                var references2 = await server.SendFindReferences(uri, 5, 9);
                var references3 = await server.SendFindReferences(uri, 10, 9);

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri, (3, 13, 3, 16), ReferenceKind.Reference),
                    (uri, (5, 4, 6, 12), ReferenceKind.Value),
                    (uri, (5, 8, 5, 11), ReferenceKind.Definition),
                    (uri, (10, 4, 11, 11), ReferenceKind.Value),
                    (uri, (10, 8, 10, 11), ReferenceKind.Definition)
                };

                references1.Should().OnlyHaveReferences(expectedReferences);
                references2.Should().OnlyHaveReferences(expectedReferences);
                references3.Should().OnlyHaveReferences(expectedReferences);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FindReferences() {
            using (var s = await CreateServerAsync()) {
                var mod1 = await TestData.CreateTestSpecificFileAsync("mod1.py", @"
def f(a):
    a.real
b = 1
f(a=b)
class C:
    real = []
    f = 2
c=C()
f(a=c)
real = None");
                await s.LoadFileAsync(mod1);

                // Add 10 blank lines to ensure the line numbers do not collide
                // We only check line numbers below, and by design we only get one
                // reference per location, so we disambiguate by ensuring mod2's
                // line numbers are larger than mod1's
                var mod2 = await s.OpenDocumentAndGetUriAsync("mod2.py", @"import mod1
" + "\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n" + @"
class D:
    real = None
    a = 1
    b = a
mod1.f(a=D)");

                // f
                var expected = new[] {
                    "Definition;(1, 4) - (1, 5)",
                    "Value;(1, 0) - (2, 10)",
                    "Reference;(4, 0) - (4, 1)",
                    "Reference;(9, 0) - (9, 1)",
                    "Reference;(16, 5) - (16, 6)"
                };
                var unexpected = new[] {
                    "Definition;(7, 4) - (7, 5)",
                };

                await AssertReferences(s, mod1, SourceLocation.MinValue, expected, unexpected, "f");
                await AssertReferences(s, mod1, new SourceLocation(2, 5), expected, unexpected);
                await AssertReferences(s, mod1, new SourceLocation(5, 2), expected, unexpected);
                await AssertReferences(s, mod1, new SourceLocation(10, 2), expected, unexpected);
                await AssertReferences(s, mod2, new SourceLocation(17, 6), expected, unexpected);

                await AssertReferences(s, mod1, new SourceLocation(8, 5), unexpected, Enumerable.Empty<string>());

                // a
                expected = new[] {
                    "Definition;(1, 6) - (1, 7)",
                    "Reference;(2, 4) - (2, 5)",
                    "Reference;(4, 2) - (4, 3)",
                    "Reference;(9, 2) - (9, 3)",
                    "Reference;(16, 7) - (16, 8)"
                };
                    unexpected = new[] {
                    "Definition;(14, 4) - (14, 5)",
                    "Reference;(15, 8) - (15, 9)"
                };
                await AssertReferences(s, mod1, new SourceLocation(3, 8), expected, unexpected, "a");
                await AssertReferences(s, mod1, new SourceLocation(2, 8), expected, unexpected);
                await AssertReferences(s, mod1, new SourceLocation(3, 5), expected, unexpected);
                await AssertReferences(s, mod1, new SourceLocation(5, 3), expected, unexpected);
                await AssertReferences(s, mod1, new SourceLocation(10, 3), expected, unexpected);
                await AssertReferences(s, mod2, new SourceLocation(17, 8), expected, unexpected);

                await AssertReferences(s, mod2, new SourceLocation(15, 5), unexpected, expected);
                await AssertReferences(s, mod2, new SourceLocation(16, 9), unexpected, expected);

                // real (in f)
                expected = new[] {
                    "Reference;(2, 6) - (2, 10)",
                    "Definition;(6, 4) - (6, 8)",
                    "Definition;(13, 4) - (13, 8)"
                };
                unexpected = new[] {
                    "Definition;(10, 0) - (10, 4)"
                };
                await AssertReferences(s, mod1, new SourceLocation(3, 5), expected, unexpected, "a.real");
                await AssertReferences(s, mod1, new SourceLocation(3, 8), expected, unexpected);

                // C.real
                expected = new[] {
                    "Definition;(10, 0) - (10, 4)",
                    "Reference;(2, 6) - (2, 10)",
                    "Reference;(6, 4) - (6, 8)"
                };
                await AssertReferences(s, mod1, new SourceLocation(7, 8), expected, Enumerable.Empty<string>());

                // D.real
                expected = new[] {
                    "Reference;(2, 6) - (2, 10)",
                    "Definition;(13, 4) - (13, 8)"
                };
                unexpected = new[] {
                    "Definition;(6, 4) - (6, 8)",
                    "Definition;(10, 0) - (10, 4)"
                };
                await AssertReferences(s, mod2, new SourceLocation(14, 8), expected, unexpected);
            }
        }

        public static async Task AssertReferences(Server s, TextDocumentIdentifier document, SourceLocation position, IEnumerable<string> contains, IEnumerable<string> excludes, string expr = null) {
            var refs = await s.FindReferences(new ReferencesParams {
                textDocument = document,
                position = position,
                _expr = expr,
                context = new ReferenceContext {
                    includeDeclaration = true,
                    _includeValues = true
                }
            }, CancellationToken.None);

            if (excludes.Any()) {
                refs.Select(r => $"{r._kind ?? ReferenceKind.Reference};{r.range}").Should().Contain(contains).And.NotContain(excludes);
            } else {
                refs.Select(r => $"{r._kind ?? ReferenceKind.Reference};{r.range}").Should().Contain(contains);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethod() {
            var text = @"
class Base(object):
    @classmethod
    def fob_base(cls): 
        pass

class Derived(Base):
    @classmethod
    def fob_derived(cls): 
        'x'

Base.fob_base()
Derived.fob_derived()
";

            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var references = await server.SendFindReferences(uri, 11, 6);

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri, (3, 8, 3, 16), ReferenceKind.Definition),
                    (uri, (2, 4, 4, 12), ReferenceKind.Value),
                    (uri, (11, 5, 11, 13), ReferenceKind.Reference),
                };

                references.Should().OnlyHaveReferences(expectedReferences);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethodInBase() {
            var text = @"
class Base(object):
    @classmethod
    def fob_base(cls): 
        pass

class Derived(Base):
    pass

Derived.fob_base()
";

            using (var server = await CreateServerAsync()) {
                var uri = await server.OpenDefaultDocumentAndGetUriAsync(text);
                var references = await server.SendFindReferences(uri, 9, 9);

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri, (3, 8, 3, 16), ReferenceKind.Definition),
                    (uri, (2, 4, 4, 12), ReferenceKind.Value),
                    (uri, (9, 8, 9, 16), ReferenceKind.Reference),
                };

                references.Should().OnlyHaveReferences(expectedReferences);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethodInImportedBase() {
            var text1 = @"
import mod2

class Derived(mod2.Base):
    pass

Derived.fob_base()
";

            var text2 = @"
class Base(object):
    @classmethod
    def fob_base(cls): 
        pass
";

            using (var server = await CreateServerAsync()) {
                var uri1 = await server.OpenDefaultDocumentAndGetUriAsync(text1);
                var uri2 = await TestData.CreateTestSpecificFileAsync("mod2.py", text2);
                await server.LoadFileAsync(uri2);

                var references = await server.SendFindReferences(uri1, 6, 9);

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri2, (3, 8, 3, 16), ReferenceKind.Definition),
                    (uri2, (2, 4, 4, 12), ReferenceKind.Value),
                    (uri1, (6, 8, 6, 16), ReferenceKind.Reference),
                };

                references.Should().OnlyHaveReferences(expectedReferences);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethodInRelativeImportedBase() {
            var text1 = @"
from .mod2 import Base

class Derived(Base):
    pass

Derived.fob_base()
";

            var text2 = @"
class Base(object):
    @classmethod
    def fob_base(cls): 
        pass
";

            using (var server = await CreateServerAsync()) {
                var uri1 = await server.OpenDefaultDocumentAndGetUriAsync(text1);
                var uri2 = await TestData.CreateTestSpecificFileAsync("mod2.py", text2);
                await server.LoadFileAsync(uri2);

                var references = await server.SendFindReferences(uri1, 6, 9);

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri2, (3, 8, 3, 16), ReferenceKind.Definition),
                    (uri2, (2, 4, 4, 12), ReferenceKind.Value),
                    (uri1, (6, 8, 6, 16), ReferenceKind.Reference),
                };

                references.Should().OnlyHaveReferences(expectedReferences);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethodInStarImportedBase() {
            var text1 = @"
from mod2 import *

class Derived(Base):
    pass

Derived.fob_base()
";

            var text2 = @"
class Base(object):
    @classmethod
    def fob_base(cls): 
        pass
";

            using (var server = await CreateServerAsync()) {
                var uri1 = await server.OpenDefaultDocumentAndGetUriAsync(text1);
                var uri2 = await TestData.CreateTestSpecificFileAsync("mod2.py", text2);
                await server.LoadFileAsync(uri2);

                var references = await server.SendFindReferences(uri1, 6, 9);

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri2, (3, 8, 3, 16), ReferenceKind.Definition),
                    (uri2, (2, 4, 4, 12), ReferenceKind.Value),
                    (uri1, (6, 8, 6, 16), ReferenceKind.Reference),
                };

                references.Should().OnlyHaveReferences(expectedReferences);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ClassMethodInRelativeImportedBaseWithCircularReference() {
            var text1 = @"
from .mod2 import Derived1

class Base(object):
    @classmethod
    def fob_base(cls): 
        pass

class Derived2(Derived1):
    pass

Derived2.fob_base()
";

            var text2 = @"
from mod1 import *

class Derived1(Base):
    pass
";

            using (var server = await CreateServerAsync()) {
                var uri1 = await TestData.CreateTestSpecificFileAsync("mod1.py", text1);
                var uri2 = await TestData.CreateTestSpecificFileAsync("mod2.py", text2);

                await server.LoadFileAsync(uri2);
                uri1 = await server.OpenDocumentAndGetUriAsync("mod1.py", text1);

                var references = await server.SendFindReferences(uri1, 11, 9);

                var expectedReferences = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri1, (5, 8, 5, 16), ReferenceKind.Definition),
                    (uri1, (4, 4, 6, 12), ReferenceKind.Value),
                    (uri1, (11, 9, 11, 17), ReferenceKind.Reference),
                };

                references.Should().OnlyHaveReferences(expectedReferences);
            }
        }

        /// <summary>
        /// Verifies that go to definition on 'self' goes to the class definition
        /// </summary>
        /// <returns></returns>
        [TestMethod, Priority(0)]
        public async Task SelfReferences() {
            var text = @"
class Base(object):
    def fob_base(self):
        pass

class Derived(Base):
    def fob_derived(self):
        self.fob_base()
        pass
";

            using (var server = await CreateServerAsync()) {
                var uri1 = await server.OpenDefaultDocumentAndGetUriAsync(text);

                var references = await server.SendFindReferences(uri1, 2, 18); // on first 'self'
                var expectedReferences1 = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri1, (1, 0, 1, 0), ReferenceKind.Definition),
                    (uri1, (2, 17, 2, 21), ReferenceKind.Reference)
                };
                references.Should().OnlyHaveReferences(expectedReferences1);

                references = await server.SendFindReferences(uri1, 7, 8); // on second 'self'
                var expectedReferences2 = new (Uri, (int, int, int, int), ReferenceKind?)[] {
                    (uri1, (5, 0, 5, 0), ReferenceKind.Definition),
                    (uri1, (6, 20, 6, 24), ReferenceKind.Reference)
                };
                references.Should().OnlyHaveReferences(expectedReferences2);
            }
        }
    }
}
