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
using System.Threading;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Documents {
    internal sealed class DocumentBuffer {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly int _ownerThreadId = Thread.CurrentThread.ManagedThreadId;

        public int Version { get; private set; }
        public string Text => _sb.ToString();

        public void Reset(int version, string content) {
            Version = version;
            _sb.Clear();
            if (!string.IsNullOrEmpty(content)) {
                _sb.Append(content);
            }
        }

        public void Update(IEnumerable<DocumentChangeSet> sets) {
            foreach (var set in sets) {
                Update(set);
            }
        }

        public void Update(DocumentChangeSet changes) {
            Check.InvalidOperation(() => _ownerThreadId == Thread.CurrentThread.ManagedThreadId,
                "Document buffer update must be done from the thread that created it");

            if (!changes.Any(c => c.WholeBuffer)) {
                if (Version >= 0) {
                    if (changes.FromVersion < Version) {
                        return;
                    } else if (changes.FromVersion > Version) {
                        throw new InvalidOperationException("missing prior versions");
                    }
                }
                if (changes.FromVersion >= changes.ToVersion) {
                    throw new InvalidOperationException("cannot reduce version without resetting buffer");
                }
            }

            int lastStart = int.MaxValue;
            var lineLoc = SplitLines(_sb).ToArray();

            foreach (var change in changes) {
                if (change.WholeBuffer) {
                    _sb.Clear();
                    if (!string.IsNullOrEmpty(change.InsertedText)) {
                        _sb.Append(change.InsertedText);
                    }
                    continue;
                }

                int start = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.Start, Text.Length);
                if (start > lastStart) {
                    throw new InvalidOperationException("changes must be in reverse order of start location");
                }
                lastStart = start;

                int end = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.End, Text.Length);
                if (end > start) {
                    Text.Remove(start, end - start);
                }
                if (!string.IsNullOrEmpty(change.InsertedText)) {
                    Text.Insert(start, change.InsertedText);
                }
            }

            Version = changes.ToVersion;
        }

        private static IEnumerable<NewLineLocation> SplitLines(StringBuilder text) {
            NewLineLocation nextLine;

            // TODO: Avoid string allocation by operating directly on StringBuilder
            var str = text.ToString();

            int lastLineEnd = 0;
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
