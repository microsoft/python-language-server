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

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Python.Analysis.Documents {
    public sealed class DocumentChangeSet: IEnumerable<DocumentChange> {
        private readonly DocumentChange[] _changes;
        public DocumentChangeSet(int fromVersion, int toVersion, IEnumerable<DocumentChange> changes) {
            FromVersion = fromVersion;
            ToVersion = toVersion;
            _changes = changes.ToArray();
        }

        public int FromVersion { get; }
        public int ToVersion { get; }

        public IEnumerator<DocumentChange> GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _changes.GetEnumerator();
    }
}
