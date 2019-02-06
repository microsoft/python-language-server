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
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Parsing.Tests {
    [TestClass]
    public class MutateStdLibTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV27() {
            TestMutateStdLib(PythonVersions.Python27_x64 ?? PythonVersions.Python27);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV35() {
            TestMutateStdLib(PythonVersions.Python35_x64 ?? PythonVersions.Python35);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV36() {
            TestMutateStdLib(PythonVersions.Python36_x64 ?? PythonVersions.Python36);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV37() {
            TestMutateStdLib(PythonVersions.Python37_x64 ?? PythonVersions.Python37);
        }

        [TestMethod, Priority(2)]
        [TestCategory("10s"), TestCategory("60s")]
        public void TestMutateStdLibV38() {
            TestMutateStdLib(PythonVersions.Python38_x64 ?? PythonVersions.Python38);
        }

        private void TestMutateStdLib(InterpreterConfiguration configuration) {
            configuration.AssertInstalled();

            for (int i = 0; i < 100; i++) {
                int seed = (int)DateTime.Now.Ticks;
                var random = new Random(seed);
                Console.WriteLine("Seed == " + seed);


                Console.WriteLine("Testing version {0} {1}", configuration.Version, configuration.LibraryPath);
                int ran = 0, succeeded = 0;
                string[] files;
                try {
                    files = Directory.GetFiles(configuration.LibraryPath);
                } catch (DirectoryNotFoundException) {
                    continue;
                }

                foreach (var file in files) {
                    try {
                        if (file.EndsWith(".py")) {
                            ran++;
                            TestOneFileMutated(file, configuration.Version.ToLanguageVersion(), random);
                            succeeded++;
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        Console.WriteLine("Failed: {0}", file);
                        break;
                    }
                }

                Assert.AreEqual(ran, succeeded);
            }
        }

        private static void TestOneFileMutated(string filename, PythonLanguageVersion version, Random random) {
            var originalText = File.ReadAllText(filename);
            int start = random.Next(originalText.Length);
            int end = random.Next(originalText.Length);

            int realStart = Math.Min(start, end);
            int length = Math.Max(start, end) - Math.Min(start, end);
            //Console.WriteLine("Removing {1} chars at {0}", realStart, length);
            originalText = originalText.Substring(realStart, length);

            ParserRoundTripTest.TestOneString(version, originalText);
        }
    }
}
