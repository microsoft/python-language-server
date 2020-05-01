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
using FluentAssertions;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class GoToDefinitionTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task BasicDefinitions() {
            const string code = @"
import os

x = os.path

class C:
    z: int
    def method(self, a, b):
        self.z = 1
        return 1.0

def func(a, b):
    a = 3
    b = a
    return 1

y = func(1, 2)
x = 1
c = C()
c.method(1, 2)
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(4, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(1, 7, 1, 9);

            reference = ds.FindDefinition(analysis, new SourceLocation(9, 9), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(7, 15, 7, 19);

            reference = ds.FindDefinition(analysis, new SourceLocation(9, 14), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(6, 4, 6, 5);

            reference = ds.FindDefinition(analysis, new SourceLocation(13, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(11, 9, 11, 10);

            reference = ds.FindDefinition(analysis, new SourceLocation(14, 9), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(11, 9, 11, 10);

            reference = ds.FindDefinition(analysis, new SourceLocation(17, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(11, 4, 11, 8);

            reference = ds.FindDefinition(analysis, new SourceLocation(18, 1), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(3, 0, 3, 1);

            reference = ds.FindDefinition(analysis, new SourceLocation(19, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(5, 6, 5, 7);

            reference = ds.FindDefinition(analysis, new SourceLocation(20, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(7, 8, 7, 14);
        }

        [TestMethod, Priority(0)]
        public async Task GotoDefinitionFromParent() {
            const string code = @"
class base:
    test: int
    def foo(self):
        pass

class child(base):
    def tmp(self):
        self.foo()
        self.test
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(9, 15), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(3, 8, 3, 11);

            reference = ds.FindDefinition(analysis, new SourceLocation(10, 15), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 4, 2, 8);
        }

        [TestMethod, Priority(0)]
        public async Task GotoDefinitionFromParentOtherModule() {
            var otherModPath = TestData.GetTestSpecificUri("other.py");
            var testModPath = TestData.GetTestSpecificUri("test.py");
            const string otherModCode = @"
class C:
    v: int
    def test(self):
        pass
";
            const string testModCode = @"
from other import C

class D(C):
    def hello(self):
        self.test();
        self.v
";

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(otherModPath, otherModCode);
            var testMod = rdt.OpenDocument(testModPath, testModCode);
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            var analysis = await testMod.GetAnalysisAsync();
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(6, 14), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("other.py");
            reference.range.Should().Be(3, 8, 3, 12);

            reference = ds.FindDefinition(analysis, new SourceLocation(7, 15), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("other.py");
            reference.range.Should().Be(2, 4, 2, 5);
        }

        [TestMethod, Priority(0)]
        public async Task GotoDefinitionFromParentOtherModuleOnSuper() {
            var otherModPath = TestData.GetTestSpecificUri("other.py");
            var testModPath = TestData.GetTestSpecificUri("test.py");
            const string otherModCode = @"
class C:
    v: int
    def test(self):
        pass
";
            const string testModCode = @"
from other import C

class D(C):
    def hello(self):
        super().test();
        super().v
";

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(otherModPath, otherModCode);
            var testMod = rdt.OpenDocument(testModPath, testModCode);
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            var analysis = await testMod.GetAnalysisAsync();
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(6, 17), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("other.py");
            reference.range.Should().Be(3, 8, 3, 12);

            reference = ds.FindDefinition(analysis, new SourceLocation(7, 17), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("other.py");
            reference.range.Should().Be(2, 4, 2, 5);
        }

        [TestMethod, Priority(0)]
        [Ignore("Todo: super() multiple Inheritance support")]
        public async Task MultipleInheritanceSuperShouldWalkDecendants() {
            var testModPath = TestData.GetTestSpecificUri("test.py");
            const string code = @"
        class GrandParent:
            def dowork(self):
                return 1

        class Dad(GrandParent):
            def dowork(self):
                return super().dowork()

        class Mom():
            def dowork(self):
                return 2

        class Child(Dad, Mom):
            def child_func(self):
                pass

        ";
            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();
            
            var testMod = rdt.OpenDocument(testModPath, code);
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            var analysis = await testMod.GetAnalysisAsync();
            var ds = new DefinitionSource(Services);

            // Goto on Dad's super().dowork() should jump to Mom's
            var reference = ds.FindDefinition(analysis, new SourceLocation(9, 33), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("test.py");
            reference.range.Should().Be(11, 16, 11, 23);
        }


        [TestMethod, Priority(0)]
        public async Task GotoModuleSource() {
            const string code = @"
import sys
import logging

logging.info('')
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 9), out _);
            reference.Should().BeNull();

            reference = ds.FindDefinition(analysis, new SourceLocation(5, 3), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 7, 2, 14);

            reference = ds.FindDefinition(analysis, new SourceLocation(3, 10), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");

            reference = ds.FindDefinition(analysis, new SourceLocation(5, 11), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceImportAs() {
            const string code = @"
import logging as log
log
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 10), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");

            reference = ds.FindDefinition(analysis, new SourceLocation(3, 2), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(1, 18, 1, 21);

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 20), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceFromImport() {
            const string code = @"from logging import A";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(1, 7), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoDefitionFromImport() {
            const string code = @"
from MultiValues import t
x = t
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 0, 2, 1);
            reference.uri.AbsolutePath.Should().Contain("MultiValues.py");

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 25), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 0, 2, 1);
            reference.uri.AbsolutePath.Should().Contain("MultiValues.py");
        }

        [TestMethod, Priority(0)]
        public async Task GotoDeclarationFromImport() {
            const string code = @"
from MultiValues import t
x = t
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DeclarationSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 5), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(1, 24, 1, 25);

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 25), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 0, 2, 1);
            reference.uri.AbsolutePath.Should().Contain("MultiValues.py");
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceFromImportAs() {
            const string code = @"from logging import RootLogger as rl";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(1, 23), out _);
            reference.Should().NotBeNull();
            reference.range.start.line.Should().BeGreaterThan(500);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoDefinitionFromImportAs() {
            const string code = @"
from logging import critical as crit
x = crit
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 6), out _);
            reference.Should().NotBeNull();
            reference.range.start.line.Should().BeGreaterThan(500);
            reference.uri.AbsolutePath.Should().Contain("logging");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoDeclarationFromImportAs() {
            const string code = @"
from logging import critical as crit
x = crit
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DeclarationSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 6), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(1, 32, 1, 36);
        }

        [TestMethod, Priority(0)]
        public async Task GotoBuiltinObject() {
            const string code = @"
class A(object):
    pass
";
            var analysis = await GetAnalysisAsync(code);

            var ds1 = new DefinitionSource(Services);
            var reference = ds1.FindDefinition(analysis, new SourceLocation(2, 12), out _);
            reference.Should().BeNull();

            var ds2 = new DeclarationSource(Services);
            reference = ds2.FindDefinition(analysis, new SourceLocation(2, 12), out _);
            reference.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task GotoRelativeImportInExplicitPackage() {
            var pkgPath = TestData.GetTestSpecificUri("pkg", "__init__.py");
            var modPath = TestData.GetTestSpecificUri("pkg", "mod.py");
            var subpkgPath = TestData.GetTestSpecificUri("pkg", "subpkg", "__init__.py");
            var submodPath = TestData.GetTestSpecificUri("pkg", "submod", "__init__.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(pkgPath, string.Empty);
            rdt.OpenDocument(modPath, "hello = 'World'");
            rdt.OpenDocument(subpkgPath, string.Empty);
            var submod = rdt.OpenDocument(submodPath, "from .. import mod");

            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            var analysis = await submod.GetAnalysisAsync(Timeout.Infinite);

            var ds = new DefinitionSource(Services);
            var reference = ds.FindDefinition(analysis, new SourceLocation(1, 18), out _);
            reference.Should().NotBeNull();
            reference.uri.Should().Be(modPath);
        }

        [TestMethod, Priority(0)]
        public async Task GotoModuleSourceFromRelativeImport() {
            var modAPath = TestData.GetTestSpecificUri("a.py");
            var modBPath = TestData.GetTestSpecificUri("b.py");

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var modA = rdt.OpenDocument(modAPath, "from .b import X");
            var modB = rdt.OpenDocument(modBPath, @"
z = 1
def X(): ...
");
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            var analysis = await modA.GetAnalysisAsync(Timeout.Infinite);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(1, 7), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("b.py");
            reference.range.Should().Be(0, 0, 0, 0);

            reference = ds.FindDefinition(analysis, new SourceLocation(1, 16), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("b.py");
            reference.range.Should().Be(2, 4, 2, 5);
        }


        [TestMethod, Priority(0)]
        public async Task EmptyAnalysis() {
            var analysis = await GetAnalysisAsync(string.Empty);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(new EmptyAnalysis(Services, analysis.Document), new SourceLocation(1, 1), out _);
            reference.Should().BeNull();

            reference = ds.FindDefinition(null, new SourceLocation(1, 1), out _);
            reference.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task ReCompile() {
            const string code = @"
import re
x = re.compile(r'hello', re.IGNORECASE)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 10), out _);
            reference.Should().NotBeNull();
            reference.range.start.line.Should().BeGreaterThan(0);
            reference.uri.AbsolutePath.Should().Contain("re.py");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task Constant() {
            const string code = @"
import helper
helper.message
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 11), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 7);
            reference.uri.AbsolutePath.Should().Contain("__init__.py");
        }

        [TestMethod, Priority(0)]
        public async Task ImportedClassMember() {
            await TestData.CreateTestSpecificFileAsync($"module{Path.DirectorySeparatorChar}bar.py", @"
class Bar:
    def get_name(self): ...
");

            const string code = @"
from .module.bar import Bar

class MainClass:
    def __init__(self):
        self.bar = Bar()

    def foo(self):
        return self.bar.get_name()
";

            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            var mainPath = TestData.GetTestSpecificUri("main.py");
            var doc = rdt.OpenDocument(mainPath, code);

            var analyzer = Services.GetService<IPythonAnalyzer>();
            await analyzer.WaitForCompleteAnalysisAsync();

            var analysis = await doc.GetAnalysisAsync(Timeout.Infinite);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(9, 27), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(2, 8, 2, 16);
            reference.uri.AbsolutePath.Should().Contain("bar.py");
        }

        [TestMethod, Priority(0)]
        public async Task NamedTuple() {
            const string code = @"
from typing import NamedTuple

Point = NamedTuple('Point', ['x', 'y'])

def f(a, b):
    return Point(a, b)

pt = Point(1, 2)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(7, 14), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(3, 0, 3, 5);
        }

        [TestMethod, Priority(0)]
        public async Task ModulePartsNavigation() {
            const string code = @"
import os.path
from os import path as os_path
print(os.path.basename('a/b/c'))
print(os_path.basename('a/b/c'))
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 9), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("os.py");

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 13), out _);
            reference.Should().NotBeNull();
            var line = File.ReadAllLines(reference.uri.AbsolutePath)[reference.range.start.line];
            line.Should().EndWith("as path");
            line.Substring(reference.range.start.character).Should().Be("path");

            reference = ds.FindDefinition(analysis, new SourceLocation(3, 7), out _);
            reference.Should().NotBeNull();
            reference.range.Should().Be(0, 0, 0, 0);
            reference.uri.AbsolutePath.Should().Contain("os.py");

            reference = ds.FindDefinition(analysis, new SourceLocation(3, 17), out _);
            reference.Should().NotBeNull();
            line = File.ReadAllLines(reference.uri.AbsolutePath)[reference.range.start.line];
            line.Should().EndWith("as path");
            line.Substring(reference.range.start.character).Should().Be("path");

            reference = ds.FindDefinition(analysis, new SourceLocation(3, 27), out _);
            reference.Should().NotBeNull();
            line = File.ReadAllLines(reference.uri.AbsolutePath)[reference.range.start.line];
            line.Should().EndWith("as path");
            line.Substring(reference.range.start.character).Should().Be("path");

            reference = ds.FindDefinition(analysis, new SourceLocation(4, 12), out _);
            reference.Should().NotBeNull();
            var osPyPath = reference.uri.AbsolutePath;
            line = File.ReadAllLines(osPyPath)[reference.range.start.line];
            line.Should().EndWith("as path");
            line.Substring(reference.range.start.character).Should().Be("path");

            reference = ds.FindDefinition(analysis, new SourceLocation(5, 12), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Be(osPyPath);
            line = File.ReadAllLines(osPyPath)[reference.range.start.line];
            line.Should().EndWith("as path");
            line.Substring(reference.range.start.character).Should().Be("path");
        }

        [TestMethod, Priority(0)]
        public async Task Unittest() {
            const string code = @"
from unittest import TestCase

class MyTestCase(TestCase):
    def test_example(self):
        with self.assertRaises(ZeroDivisionError):
            value = 1 / 0
        self.assertNotEqual(value, 1)
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(6, 24), out _);
            reference.Should().NotBeNull();
            reference.range.start.line.Should().BeGreaterThan(0);
            reference.uri.AbsolutePath.Should().Contain("case.py");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task DateTimeProperty() {
            const string code = @"
import datetime
x = datetime.datetime.day
";
            var analysis = await GetAnalysisAsync(code, PythonVersions.LatestAvailable3X);
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 15), out _);
            reference.Should().NotBeNull();
            reference.range.start.line.Should().BeGreaterThan(0);
            reference.uri.AbsolutePath.Should().Contain("datetime.py");
            reference.uri.AbsolutePath.Should().NotContain("pyi");
        }

        [TestMethod, Priority(0)]
        public async Task GotoDefinitionFromMultiImport() {
            var otherModPath = TestData.GetTestSpecificUri("other.py");
            var testModPath = TestData.GetTestSpecificUri("test.py");
            const string otherModCode = @"
def a(): ...
def b(): ...
def c(): ...
";
            const string testModCode = @"
from other import a, b, c
";
            await CreateServicesAsync(PythonVersions.LatestAvailable3X);
            var rdt = Services.GetService<IRunningDocumentTable>();

            rdt.OpenDocument(otherModPath, otherModCode);
            var testMod = rdt.OpenDocument(testModPath, testModCode);
            await Services.GetService<IPythonAnalyzer>().WaitForCompleteAnalysisAsync();
            var analysis = await testMod.GetAnalysisAsync();
            var ds = new DefinitionSource(Services);

            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 19), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("other.py");
            reference.range.Should().Be(1, 4, 1, 5);

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 22), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("other.py");
            reference.range.Should().Be(2, 4, 2, 5);

            reference = ds.FindDefinition(analysis, new SourceLocation(2, 25), out _);
            reference.Should().NotBeNull();
            reference.uri.AbsolutePath.Should().Contain("other.py");
            reference.range.Should().Be(3, 4, 3, 5);
        }

        [TestMethod, Priority(0)]
        public async Task UnknownType() {
            const string code = @"
A
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);
            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 1), out _);
            reference.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task UnknownImportedType() {
            const string code = @"
from nonexistent import some
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);
            var reference = ds.FindDefinition(analysis, new SourceLocation(2, 26), out _);
            reference.Should().BeNull();
        }

        [TestMethod, Priority(0)]
        public async Task LocalParameter() {
            const string code = @"
def func(a, b):
    return a+b
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);
            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 12), out _);
            reference.range.Should().Be(1, 9, 1, 10);
        }

        [TestMethod, Priority(0)]
        public async Task CompiledCode() {
            const string code = @"
import os
os.mkdir()
";
            var analysis = await GetAnalysisAsync(code);
            var ds = new DefinitionSource(Services);
            var reference = ds.FindDefinition(analysis, new SourceLocation(3, 6), out _);
            reference.range.start.line.Should().NotBe(1);
            reference.range.end.line.Should().NotBe(1);
            reference.uri.AbsolutePath.Should().Contain(".pyi");
        }
    }
}
