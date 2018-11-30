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
using System.IO;
using System.Text;
using Microsoft.Python.Core.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Parsing.Tests {
    /// <summary>
    /// Test cases to verify that the tokenizer successfully preserves all information for round tripping source code.
    /// </summary>
    [TestClass]
    public class TokenizerRoundTripTest {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        // TODO: Add an explicit test for grouping characters and white space, e.g.:
        // (a, b, [whitespace]
        //  [more whitespace]   c, d)
        //
        [TestMethod, Priority(0)]
        public void SimpleTest() {
            var versions = new[] { 
                new { Path = "C:\\Python25\\Lib", Version = PythonLanguageVersion.V25 },
                new { Path = "C:\\Python26\\Lib", Version = PythonLanguageVersion.V26 },
                new { Path = "C:\\Python27\\Lib", Version = PythonLanguageVersion.V27 },
                
                new { Path = "C:\\Python30\\Lib", Version = PythonLanguageVersion.V30 },
                new { Path = "C:\\Python31\\Lib", Version = PythonLanguageVersion.V31 },
                new { Path = "C:\\Python32\\Lib", Version = PythonLanguageVersion.V32 },
                new { Path = "C:\\Python33\\Lib", Version = PythonLanguageVersion.V33 }
            };

            foreach (var optionSet in new[] { TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins, TokenizerOptions.Verbatim }) {
                foreach (var version in versions) {
                    Console.WriteLine("Testing version {0} {1} w/ Option Set {2}", version.Version, version.Path, optionSet);
                    int ran = 0, succeeded = 0;
                    string[] files;
                    try {
                        files = Directory.GetFiles(version.Path);
                    } catch (DirectoryNotFoundException) {
                        continue;
                    }

                    foreach (var file in files) {
                        try {
                            if (file.EndsWith(".py")) {
                                ran++;
                                TestOneFile(file, version.Version, optionSet);
                                succeeded++;
                            }
                        } catch (Exception e) {
                            Console.WriteLine(e);
                            Console.WriteLine("Failed: {0}", file);
                        }
                    }

                    Assert.AreEqual(ran, succeeded);
                }
            }
        }

        struct ExpectedToken {
            public readonly TokenKind Kind;
            public readonly IndexSpan Span;
            public readonly string Image;

            public ExpectedToken(TokenKind kind, IndexSpan span, string image) {
                Kind = kind;
                Span = span;
                Image = image;
            }
        }

        [TestMethod, Priority(0)]
        public void TrailingBackSlash() {
            var tokens = TestOneString(
                PythonLanguageVersion.V27, 
                TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins,
                $"fob{Environment.NewLine}\\"
            );
            AssertEqualTokens(
                tokens, 
                new[] { 
                    new ExpectedToken(TokenKind.Name, new IndexSpan(0, 3), "fob"), 
                    new ExpectedToken(TokenKind.NewLine, new IndexSpan(3, Environment.NewLine.Length), Environment.NewLine), 
                    new ExpectedToken(TokenKind.EndOfFile, new IndexSpan(3 + Environment.NewLine.Length, 1), "\\"),
                }
            );

            tokens = TestOneString(
                PythonLanguageVersion.V27,
                TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins,
                $"fob{Environment.NewLine}\\b"
            );
            AssertEqualTokens(
                tokens,
                new[] { 
                    new ExpectedToken(TokenKind.Name, new IndexSpan(0, 3), "fob"), 
                    new ExpectedToken(TokenKind.NewLine, new IndexSpan(3, Environment.NewLine.Length), Environment.NewLine),
                    new ExpectedToken(TokenKind.Error, new IndexSpan(3 + Environment.NewLine.Length, 1), "\\"), 
                    new ExpectedToken(TokenKind.Name, new IndexSpan(4 + Environment.NewLine.Length, 1), "b")
                }
            );
        }

        private static void AssertEqualTokens(List<TokenWithSpan> tokens, ExpectedToken[] expectedTokens) {
            try {
                Assert.AreEqual(expectedTokens.Length, tokens.Count);
                for (int i = 0; i < tokens.Count; i++) {
                    Assert.AreEqual(expectedTokens[i].Kind, tokens[i].Token.Kind);
                    Assert.AreEqual(expectedTokens[i].Span, tokens[i].Span);
                    Assert.AreEqual(expectedTokens[i].Image, tokens[i].Token.VerbatimImage);
                }
            } finally {
                foreach (var token in tokens) {
                    Console.WriteLine("new ExpectedToken(TokenKind.{0}, new IndexSpan({1}, {2}), \"{3}\"), ",
                        token.Token.Kind,
                        token.Span.Start,
                        token.Span.Length,
                        token.Token.VerbatimImage
                    );
                }
            }
        }

        [TestMethod, Priority(0)]
        public void BinaryTest() {
            var filename = TestData.GetPath("TestData", "random.bin");
            TestOneFile(filename, PythonLanguageVersion.V27, TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins);
            TestOneFile(filename, PythonLanguageVersion.V27, TokenizerOptions.Verbatim);
        }

        [TestMethod, Priority(0)]
        public void TestErrors() {
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim, "http://xkcd.com/353/\")");
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins, "http://xkcd.com/353/\")");
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim | TokenizerOptions.VerbatimCommentsAndLineJoins, "lambda, U+039B");
            TestOneString(PythonLanguageVersion.V27, TokenizerOptions.Verbatim, "lambda, U+039B");
        }

        private static void TestOneFile(string filename, PythonLanguageVersion version, TokenizerOptions optionSet) {
            var originalText = File.ReadAllText(filename);

            TestOneString(version, optionSet, originalText);
        }

        private static List<TokenWithSpan> TestOneString(PythonLanguageVersion version, TokenizerOptions optionSet, string originalText) {
            StringBuilder output = new StringBuilder();

            var tokenizer = new Tokenizer(version, options: optionSet);
            tokenizer.Initialize(new StringReader(originalText));
            Token token;
            int prevOffset = 0;

            List<TokenWithSpan> tokens = new List<TokenWithSpan>();
            while ((token = tokenizer.GetNextToken()) != Tokens.EndOfFileToken) {
                tokens.Add(new TokenWithSpan(token, tokenizer.TokenSpan));

                output.Append(tokenizer.PreceedingWhiteSpace);
                output.Append(token.VerbatimImage);

                const int contextSize = 50;
                for (int i = prevOffset; i < originalText.Length && i < output.Length; i++) {
                    if (originalText[i] != output[i]) {
                        // output some context
                        StringBuilder x = new StringBuilder();
                        StringBuilder y = new StringBuilder();
                        StringBuilder z = new StringBuilder();
                        for (int j = Math.Max(0, i - contextSize); j < Math.Min(Math.Min(originalText.Length, output.Length), i + contextSize); j++) {
                            x.AppendRepr(originalText[j]);
                            y.AppendRepr(output[j]);
                            if (j == i) {
                                z.Append("^");
                            } else {
                                z.Append(" ");
                            }
                        }

                        Console.WriteLine("Mismatch context at {0}:", i);
                        Console.WriteLine("Original: {0}", x.ToString());
                        Console.WriteLine("New     : {0}", y.ToString());
                        Console.WriteLine("Differs : {0}", z.ToString());
                        Console.WriteLine("Token   : {0}", token);

                        Assert.AreEqual(originalText[i], output[i], String.Format("Characters differ at {0}, got {1}, expected {2}", i, output[i], originalText[i]));
                    }
                }

                prevOffset = output.Length;
            }
            output.Append(tokenizer.PreceedingWhiteSpace);

            Assert.AreEqual(originalText.Length, output.Length);
            return tokens;
        }
    }

    static class StringBuilderExtensions {
        public static void AppendRepr(this StringBuilder self, char ch) {
            switch (ch) {
                // we append funky characters unlikely to show up here just so we always append a single char and it's easier to compare strings
                case '\t': self.Append("»"); break;
                case '\r': self.Append("¬"); break;
                case '\n': self.Append("‼"); break;
                case '\f': self.Append("╢"); break;
                case (char)0: self.Append(' '); break;
                default:
                    self.Append(ch); break;
            }
        }
    }
}
