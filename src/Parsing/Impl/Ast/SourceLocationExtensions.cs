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

using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    public static class SourceLocationExtensions {
        public static int ToIndex(this SourceLocation location, ILocationConverter lc) => lc.LocationToIndex(location);
    }

    public static class SourceSpanExtensions {
        public static IndexSpan ToIndexSpan(this SourceSpan span, ILocationConverter lc)
            => IndexSpan.FromBounds(lc.LocationToIndex(span.Start), lc.LocationToIndex(span.End));
        public static IndexSpan ToIndexSpan(this Range range, ILocationConverter lc)
            => IndexSpan.FromBounds(lc.LocationToIndex(range.start), lc.LocationToIndex(range.end));
    }

    public static class IndexSpanExtensions {
        public static SourceSpan ToSourceSpan(this IndexSpan span, ILocationConverter lc)
            => lc != null ? new SourceSpan(lc.IndexToLocation(span.Start), lc.IndexToLocation(span.End)) : default;
    }
}
