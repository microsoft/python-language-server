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
using System.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Documents {
    internal sealed class DocumentBuffer {
        private readonly object _lock = new object();
        private StringBuilder _sb;
        private string _content;

        public int Version { get; private set; }

        public string Text {
            get {
                lock (_lock) {
                    if (_content == null) {
                        _content = _sb?.ToString() ?? string.Empty;
                    }
                    return _content;
                }
            }
        }

        public void Reset(int version, string content) {
            lock (_lock) {
                Version = version;
                _content = content ?? string.Empty;
                _sb = null;
            }
        }

        public void Update(IEnumerable<DocumentChange> changes) {
            lock (_lock) {
                _sb = _sb ?? new StringBuilder(_content);

                var lastStart = int.MaxValue;
                var lineLoc = SplitLines(_sb).ToArray();

                foreach (var change in changes) {
                    if (change.ReplaceAllText) {
                        _sb = new StringBuilder(change.InsertedText);
                        lastStart = int.MaxValue;
                        lineLoc = SplitLines(_sb).ToArray();
                        continue;
                    }

                    var start = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.Start, sb.Length);
                    if (start > lastStart) {
                        throw new InvalidOperationException("changes must be in reverse order of start location");
                    }
                    lastStart = start;

                    var end = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.End, _sb.Length);
                    if (end > start) {
                        _sb.Remove(start, end - start);
                    }
                    if (!string.IsNullOrEmpty(change.InsertedText)) {
                        _sb.Insert(start, change.InsertedText);
                    }
                }

                Version++;
                _content = null;
            }
        }

        private static IEnumerable<NewLineLocation> SplitLines(StringBuilder text) {
            NewLineLocation nextLine;

            // TODO: Avoid string allocation by operating directly on StringBuilder
            var str = text.ToString();

            var lastLineEnd = 0;
            while ((nextLine = NewLineLocation.FindNewLine(str, lastLineEnd)).EndIndex != lastLineEnd) {
                yield return nextLine;
                lastLineEnd = nextLine.EndIndex;
            }

            if (lastLineEnd != str.Length) {
                yield return nextLine;
            }
        }
    }
}
