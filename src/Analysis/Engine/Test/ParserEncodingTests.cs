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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.IO;
using System.Text;
using Microsoft.Python.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    /// <summary>
    /// Test cases for parser written in a continuation passing style.
    /// </summary>
    [TestClass]
    public class ParserEncodingTests {
        #region Test Cases

        [TestMethod, Priority(0)]
        public void GetEncodingFromStream() {
            var encoding = Parser.GetEncodingFromStream(MakeStream(new byte[0]));
            Assert.AreEqual(encoding.EncodingName, Encoding.UTF8.EncodingName);

            //encoding = Parser.GetEncodingFromStream(MakeStream(new byte[] { 0xFF, 0xFE }));
            //Assert.AreEqual(encoding.EncodingName, Encoding.Unicode.EncodingName);

            //encoding = Parser.GetEncodingFromStream(MakeStream(new byte[] { 0xFF, 0xFE, 0, 0 }));
            //Assert.AreEqual(encoding.EncodingName, Encoding.UTF32.EncodingName);

            encoding = Parser.GetEncodingFromStream(MakeStream(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.AreEqual(encoding.EncodingName, Encoding.UTF8.EncodingName);
        }

        #endregion

        private static Stream MakeStream(byte[] content) 
            => new MemoryStream(content);
    }
}
