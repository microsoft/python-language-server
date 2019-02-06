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
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
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
    }
}
