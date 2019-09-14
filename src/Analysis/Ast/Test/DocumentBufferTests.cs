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
using System.Reflection;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class DocumentBufferTests {
        [TestMethod, Priority(0)]
        public void BasicDocumentBuffer() {
            var doc = new DocumentBuffer();
            doc.Reset(0, @"def f(x):
    return

def g(y):
    return y * 2
");

            doc.Update(new List<DocumentChange> {
                // We *should* batch adjacent insertions, but we should also
                // work fine even if we don't. Note that the insertion point
                // tracks backwards with previous insertions in the same version.
                // If each of these were in its own array, the location would
                // have to change for each.
                DocumentChange.Insert(")", new SourceLocation(2, 11)),
                DocumentChange.Insert("x", new SourceLocation(2, 11)),
                DocumentChange.Insert("(", new SourceLocation(2, 11)),
                DocumentChange.Insert("g", new SourceLocation(2, 11)),
                DocumentChange.Insert(" ", new SourceLocation(2, 11))
            });

            doc.Text.Should().Contain("return g(x)");
            Assert.AreEqual(1, doc.Version);

            doc.Update(new[] {
                DocumentChange.Delete(new SourceLocation(2, 14), new SourceLocation(2, 15)),
                DocumentChange.Insert("x * 2", new SourceLocation(2, 14))
            });

            doc.Text.Should().Contain("return g(x * 2)");

            doc.Update(new[] {
                DocumentChange.Replace(new SourceLocation(2, 18), new SourceLocation(2, 19), "300")
            });

            doc.Text.Should().Contain("return g(x * 300)");

            doc.Update(new[] {
                // Changes are out of order, but we should fix that automatically
                DocumentChange.Delete(new SourceLocation(2, 13), new SourceLocation(2, 22)),
                DocumentChange.Insert("#", new SourceLocation(2, 7))
            });
            doc.Text.Should().Contain("re#turn g");
        }

        [TestMethod, Priority(0)]
        public void ResetDocumentBuffer() {
            var doc = new DocumentBuffer();

            doc.Reset(0, string.Empty);
            Assert.AreEqual(string.Empty, doc.Text);

            doc.Update(new[] {
                DocumentChange.Insert("text", SourceLocation.MinValue)
            });

            Assert.AreEqual("text", doc.Text);
            Assert.AreEqual(1, doc.Version);

            doc.Reset(0, @"abcdef");

            Assert.AreEqual(@"abcdef", doc.Text);
            Assert.AreEqual(0, doc.Version);
        }

        [TestMethod, Priority(0)]
        public void ReplaceAllDocumentBuffer() {
            var doc = new DocumentBuffer();

            doc.Reset(0, string.Empty);
            Assert.AreEqual(string.Empty, doc.Text);

            doc.Update(new[] {
                DocumentChange.ReplaceAll("text")
            });

            Assert.AreEqual("text", doc.Text);
            Assert.AreEqual(1, doc.Version);

            doc.Update(new[] {
                DocumentChange.ReplaceAll("abcdef")
            });

            Assert.AreEqual(@"abcdef", doc.Text);
            Assert.AreEqual(2, doc.Version);

            doc.Update(new[] {
                DocumentChange.Insert("text", SourceLocation.MinValue),
                DocumentChange.ReplaceAll("1234")
            });

            Assert.AreEqual(@"1234", doc.Text);
            Assert.AreEqual(3, doc.Version);

            doc.Update(new[] {
                DocumentChange.ReplaceAll("1234"),
                DocumentChange.Insert("text", SourceLocation.MinValue)
            });

            Assert.AreEqual(@"text1234", doc.Text);
            Assert.AreEqual(4, doc.Version);
        }

        [TestMethod, Priority(0)]
        public void DeleteMultipleDisjoint() {
            var doc = new DocumentBuffer();
            doc.Reset(0, @"
line1
line2
line3
line4
");
            doc.Update(new[] {
                DocumentChange.Delete(new SourceSpan(5, 5, 5, 6)),
                DocumentChange.Delete(new SourceSpan(4, 5, 4, 6)),
                DocumentChange.Delete(new SourceSpan(3, 5, 3, 6)),
                DocumentChange.Delete(new SourceSpan(2, 5, 2, 6))
            });
            Assert.AreEqual(@"
line
line
line
line
", doc.Text);
        }

        [TestMethod, Priority(0)]
        public void InsertMultipleDisjoint() {
            var doc = new DocumentBuffer();
            doc.Reset(0, @"
line
line
line
line
");
            doc.Update(new[] {
                DocumentChange.Insert("4", new SourceLocation(5, 5)),
                DocumentChange.Insert("3", new SourceLocation(4, 5)),
                DocumentChange.Insert("2", new SourceLocation(3, 5)),
                DocumentChange.Insert("1", new SourceLocation(2, 5)),
            });
            Assert.AreEqual(@"
line1
line2
line3
line4
", doc.Text);
        }

        [TestMethod, Priority(0)]
        public void DeleteAcrossLines() {
            var doc = new DocumentBuffer();
            doc.Reset(0, @"
line1
line2
line3
line4
");
            doc.Update(new[] {
                DocumentChange.Delete(new SourceSpan(4, 5, 5, 5)),
                DocumentChange.Delete(new SourceSpan(2, 5, 3, 5)),
            });
            Assert.AreEqual(@"
line2
line4
", doc.Text);
        }

        [TestMethod, Priority(0)]
        public void SequentialChanges() {
            var doc = new DocumentBuffer();
            doc.Reset(0, @"
line1
line2
line3
line4
");
            doc.Update(new[] {
                DocumentChange.Delete(new SourceSpan(2, 5, 3, 5)),
                DocumentChange.Delete(new SourceSpan(3, 5, 4, 5))
            });
            Assert.AreEqual(@"
line2
line4
", doc.Text);
        }

        [TestMethod, Priority(0)]
        public void InsertTopToBottom() {
            var doc = new DocumentBuffer();
            doc.Reset(0, @"linelinelineline");
            doc.Update(new[] {
                DocumentChange.Insert("\n", new SourceLocation(1, 1)),
                DocumentChange.Insert("1\n", new SourceLocation(2, 5)),
                DocumentChange.Insert("2\r", new SourceLocation(3, 5)),
                DocumentChange.Insert("3\r\n", new SourceLocation(4, 5)),
                DocumentChange.Insert("4\r\n", new SourceLocation(5, 5)),
            });
            Assert.AreEqual("\nline1\nline2\rline3\r\nline4\r\n", doc.Text);
        }

        [NewLineTestData]
        [DataTestMethod, Priority(0)]
        public void NewLines(string s, NewLineLocation[] expected) {
            var doc = new DocumentBuffer();
            doc.Reset(0, s);
            var nls = doc.SplitLines().ToArray();
            for (var i = 0; i < nls.Length; i++) {
                Assert.AreEqual(nls[i].Kind, expected[i].Kind);
                Assert.AreEqual(nls[i].EndIndex, expected[i].EndIndex);
            }
        }

        private sealed class NewLineTestDataAttribute : Attribute, ITestDataSource {
            public IEnumerable<object[]> GetData(MethodInfo methodInfo) =>
                new List<object[]> {
                    new object[] {
                        string.Empty,
                        new NewLineLocation[] {
                            new NewLineLocation(0, NewLineKind.None),
                        }
                    },
                    new object[] {
                        "\r\r",
                        new NewLineLocation[] {
                            new NewLineLocation(1, NewLineKind.CarriageReturn),
                            new NewLineLocation(2, NewLineKind.CarriageReturn)
                        }
                    },
                    new object[] {
                        "\n\n",
                        new NewLineLocation[] {
                            new NewLineLocation(1, NewLineKind.LineFeed),
                            new NewLineLocation(2, NewLineKind.LineFeed)
                        }
                    },
                    new object[] {
                        "\r\n\n\r",
                        new NewLineLocation[] {
                            new NewLineLocation(2, NewLineKind.CarriageReturnLineFeed),
                            new NewLineLocation(3, NewLineKind.LineFeed),
                            new NewLineLocation(4, NewLineKind.CarriageReturn)
                        }
                    },
                    new object[] {
                        "a\r\nb\r\n c\r d\ne",
                        new NewLineLocation[] {
                            new NewLineLocation(3, NewLineKind.CarriageReturnLineFeed),
                            new NewLineLocation(6, NewLineKind.CarriageReturnLineFeed),
                            new NewLineLocation(9, NewLineKind.CarriageReturn),
                            new NewLineLocation(12, NewLineKind.LineFeed),
                            new NewLineLocation(13, NewLineKind.None)
                        }
                    }
                };
            public string GetDisplayName(MethodInfo methodInfo, object[] data)
                => data != null ? $"{methodInfo.Name} ({FormatName((string)data[0])})" : null;

            private static string FormatName(string s) => s.Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
