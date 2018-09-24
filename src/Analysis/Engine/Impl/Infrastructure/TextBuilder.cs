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

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    internal class TextBuilder {
        private readonly List<string> _segments = new List<string>();

        public override string ToString() {
            IEnumerable<string> seq = _segments;
            if (IsLastWhitespace()) {
                // Don't mutate when producing the string.
                seq = _segments.Take(_segments.Count - 1);
            }
            return string.Concat(seq);
        }

        public void Append(object o) => _segments.Add(o.ToString());

        /// <summary>
        /// "Soft" append a space to the text. If the most recent appended item
        /// was whitespace, then nothing is added.
        /// </summary>
        /// <param name="count">How many spaces to append.</param>
        /// <param name="allowLeading">True if spacing can be appended as the first item.</param>
        public void SoftAppendSpace(int count = 1, bool allowLeading = false) {
            if (_segments.Count == 0 && !allowLeading) {
                return;
            }

            if (IsLastWhitespace()) {
                count--;
            }

            _segments.Add(new string(' ', count));
        }

        private bool IsLastWhitespace() => _segments.Count != 0 && string.IsNullOrWhiteSpace(_segments.Last());
    }
}
