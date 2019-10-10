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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Documents {
    internal sealed class DocumentBuffer {
        private readonly object _lock = new object();
        private StringBuilder _sb = new StringBuilder();
        private string _content;
        private bool _cleared;
        private bool _initialized;

        public int Version { get; private set; }

        public string Text {
            get {
                lock (_lock) {
                    return _content ?? (_content = _sb.ToString());
                }
            }
        }

        public void SetContent(string content) {
            lock (_lock) {
                Check.InvalidOperation(!_initialized, "Buffer is already initialized.");
                if (_cleared) {
                    return; // User may try and edit library file where we have already dropped the content.
                }
                Version = 0;
                _content = content ?? string.Empty;
                _sb = null;
                _initialized = true;
            }
        }

        public void Clear() {
            lock (_lock) {
                _content = string.Empty;
                _sb = null;
                _cleared = true;
            }
        }

        public void MarkChanged() {
            lock (_lock) {
                Check.InvalidOperation(_initialized, "Buffer is not initialized.");
                if(_cleared) {
                    return; // User may try and edit library file where we have already dropped the content.
                }
                Version++;
            }
        }

        public void Update(IEnumerable<DocumentChange> changes) {
            lock (_lock) {
                Check.InvalidOperation(_initialized, "Buffer is not initialized.");
                if (_cleared) {
                    return; // User may try and edit library file where we have already dropped the content.
                }

                _sb = _sb ?? new StringBuilder(_content);

                foreach (var change in changes) {
                    // Every change may change where the lines end so in order 
                    // to correctly determine line/offsets we must re-split buffer
                    // into lines after each change.
                    var lineLoc = GetNewLineLocations().ToArray();

                    if (change.ReplaceAllText) {
                        _sb = new StringBuilder(change.InsertedText);
                        continue;
                    }

                    var start = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.Start, _sb.Length);
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

        public IEnumerable<NewLineLocation> GetNewLineLocations() {
            lock (_lock) {
                _sb = _sb ?? new StringBuilder(_content); // for tests

                if (_sb.Length == 0) {
                    yield return new NewLineLocation(0, NewLineKind.None);
                }

                for (var i = 0; i < _sb.Length; i++) {
                    var ch = _sb[i];
                    var nextCh = i < _sb.Length - 1 ? _sb[i + 1] : '\0';
                    switch (ch) {
                        case '\r' when nextCh == '\n':
                            i++;
                            yield return new NewLineLocation(i + 1, NewLineKind.CarriageReturnLineFeed);
                            break;
                        case '\n':
                            yield return new NewLineLocation(i + 1, NewLineKind.LineFeed);
                            break;
                        case '\r':
                            yield return new NewLineLocation(i + 1, NewLineKind.CarriageReturn);
                            break;
                        default:
                            if (i == _sb.Length - 1) {
                                yield return new NewLineLocation(i + 1, NewLineKind.None);
                            }
                            break;
                    }
                }
            }
        }
    }
}
