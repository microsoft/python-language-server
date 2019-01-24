using FluentAssertions;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis.Indexing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using TestUtilities;

namespace AnalysisTests.FluentAssertions {
    class IndexFileParserAssertions {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }
    }
}
