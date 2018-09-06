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

using Microsoft.PythonTools.Analysis;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed class DocumentReader : IDocumentReader {
        private readonly IDocument _doc;
        private readonly int _part;
        private string _content;
        private bool _read;

        public DocumentReader(IDocument doc, int part) {
            _doc = doc;
            _part = part;
        }

        public string ReadToEnd() => Content;

        public string Read(int start, int count) => Content?.Substring(start, count);

        private string Content {
            get {
                if (_content == null && !_read) {
                    var reader = _doc.ReadDocument(_part, out _);
                    _read = true;
                    if (reader == null) {
                        return null;
                    }
                    using (reader) {
                        _content = reader.ReadToEnd();
                    }
                }
                return _content;
            }
        }
    }
}
