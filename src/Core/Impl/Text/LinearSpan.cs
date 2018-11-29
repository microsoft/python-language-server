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

using System;
using System.Diagnostics;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Core.Text {
    public class LinearSpan: IEquatable<LinearSpan> {
        /// <summary>
        /// Returns an empty, invalid span.
        /// </summary>
        public static LinearSpan EmptySpan { get; } = new LinearSpan(0, 0);

        /// <summary>
        /// Creates span starting at position 0 and length of 0
        /// </summary>
        [DebuggerStepThrough]
        public LinearSpan() : this(0) { }

        /// <summary>
        /// Creates span starting at given position and length of zero.
        /// </summary>
        /// <param name="position">Start position</param>
        [DebuggerStepThrough]
        public LinearSpan(int position) {
            Start = position;
            End = position < int.MaxValue ? position + 1 : position;
        }

        /// <summary>
        /// Creates span based on start and end positions.
        /// End is exclusive, Length = End - Start
        /// <param name="start">Span start</param>
        /// <param name="length">Span length</param>
        /// </summary>
        [DebuggerStepThrough]
        public LinearSpan(int start, int length) {
            Check.Argument(nameof(length), () => length >= 0);
            Start = start;
            End = start + length;
        }

        /// <summary>
        /// Creates span based on another span
        /// </summary>
        /// <param name="span">Span to use as position source</param>
        public LinearSpan(LinearSpan span) : this(span.Start, span.Length) { }

        /// <summary>
        /// Resets span to (0, 0)
        /// </summary>
        [DebuggerStepThrough]
        public void Empty() {
            Start = 0;
            End = 0;
        }

        /// <summary>
        /// Creates span based on start and end positions.
        /// End is exclusive, Length = End - Start
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        [DebuggerStepThrough]
        public static LinearSpan FromBounds(int start, int end) => new LinearSpan(start, end - start);

        /// <summary>
        /// Finds out of span intersects another span
        /// </summary>
        /// <param name="start">Start of another span</param>
        /// <param name="length">Length of another span</param>
        /// <returns>True if spans intersect</returns>
        [DebuggerStepThrough]
        public virtual bool Intersect(int start, int length) => Intersect(this, start, length);

        /// <summary>
        /// Finds out of span intersects another span
        /// </summary>
        /// <param name="span">span</param>
        /// <returns>True if spans intersect</returns>
        [DebuggerStepThrough]
        public virtual bool Intersect(LinearSpan span) => Intersect(this, span.Start, span.Length);

        /// <summary>
        /// Finds out if span represents valid span (it's length is greater than zero)
        /// </summary>
        /// <returns>True if span is valid</returns>
        [DebuggerStepThrough]
        public virtual bool IsValid() => IsValid(this);

        #region LinearSpan
        /// <summary>
        /// span start position
        /// </summary>
        public int Start { get; private set; }

        /// <summary>
        /// span end position (excluded)
        /// </summary>
        public int End { get; private set; }

        /// <summary>
        /// span length
        /// </summary>
        public int Length => End - Start;

        /// <summary>
        /// Determines if span contains given position
        /// </summary>
        [DebuggerStepThrough]
        public virtual bool Contains(int position) => Contains(this, position);
        #endregion

        /// <summary>
        /// Determines if span fully contains another span
        /// </summary>
        [DebuggerStepThrough]
        public virtual bool Contains(LinearSpan span) => Contains(span.Start) && Contains(span.End);

        /// <summary>
        /// Finds out if span represents valid span (when span is not null and it's length is greater than zero)
        /// </summary>
        /// <returns>True if span is valid</returns>
        [DebuggerStepThrough]
        public static bool IsValid(LinearSpan span) => span != null && span.Length > 0;

        /// <summary>
        /// Determines if span contains given position
        /// </summary>
        /// <returns>True if position is inside the span</returns>
        [DebuggerStepThrough]
        public static bool Contains(LinearSpan span, int position) => Contains(span.Start, span.Length, position);

        /// <summary>
        /// Determines if span contains another span
        /// </summary>
        [DebuggerStepThrough]
        public static bool Contains(LinearSpan span, LinearSpan other) => span.Contains(other.Start) && span.Contains(other.End);

        /// <summary>
        /// Determines if span contains given position
        /// </summary>
        /// <param name="spanStart">Start of the span</param>
        /// <param name="spanLength">Length of the span</param>
        /// <param name="position">Position</param>
        /// <returns>True if position is inside the span</returns>
        public static bool Contains(int spanStart, int spanLength, int position) {
            if (spanLength == 0 && position == spanStart) {
                return true;
            }
            return position >= spanStart && position < spanStart + spanLength;
        }

        /// <summary>
        /// Calculates span that includes both supplied spans.
        /// </summary>
        public static LinearSpan Union(LinearSpan span1, LinearSpan span2) {
            var start = Math.Min(span1.Start, span2.Start);
            var end = Math.Max(span1.End, span2.End);

            return start <= end ? FromBounds(start, end) : EmptySpan;
        }

        /// <summary>
        /// Calculates span that includes both supplied spans.
        /// </summary>
        public static LinearSpan Union(LinearSpan span1, int spanStart, int spanLength) {
            var start = Math.Min(span1.Start, spanStart);
            var end = Math.Max(span1.End, spanStart + spanLength);

            return start <= end ? FromBounds(start, end) : EmptySpan;
        }

        /// <summary>
        /// Finds out if span intersects another span
        /// </summary>
        /// <param name="span1">First text span</param>
        /// <param name="span2">Second text span</param>
        /// <returns>True if spans intersect</returns>
        [DebuggerStepThrough]
        public static bool Intersect(LinearSpan span1, LinearSpan span2)
            => Intersect(span1, span2.Start, span2.Length);

        /// <summary>
        /// Finds out if span intersects another span
        /// </summary>
        /// <param name="span">First text span</param>
        /// <param name="spanStart2">Start of the second span</param>
        /// <param name="spanLength2">Length of the second span</param>
        /// <returns>True if spans intersect</returns>
        [DebuggerStepThrough]
        public static bool Intersect(LinearSpan span1, int spanStart2, int spanLength2)
            => Intersect(span1.Start, span1.Length, spanStart2, spanLength2);

        /// <summary>
        /// Finds out if span intersects another span
        /// </summary>
        /// <param name="spanStart1">Start of the first span</param>
        /// <param name="spanLength1">Length of the first span</param>
        /// <param name="spanStart2">Start of the second span</param>
        /// <param name="spanLength2">Length of the second span</param>
        /// <returns>True if spans intersect</returns>
        public static bool Intersect(int spanStart1, int spanLength1, int spanStart2, int spanLength2) {
            // !(spanEnd2 <= spanStart1 || spanStart2 >= spanEnd1)

            // Support intersection with empty spans

            if (spanLength1 == 0 && spanLength2 == 0) {
                return spanStart1 == spanStart2;
            }

            if (spanLength1 == 0) {
                return Contains(spanStart2, spanLength2, spanStart1);
            }

            if (spanLength2 == 0) {
                return Contains(spanStart1, spanLength1, spanStart2);
            }

            return spanStart2 + spanLength2 > spanStart1 && spanStart2 < spanStart1 + spanLength1;
        }

        /// <summary>
        /// Calculates span that is an intersection of the supplied spans.
        /// </summary>
        /// <returns>Intersection or empty span if spans don't intersect</returns>
        public static LinearSpan Intersection(LinearSpan span1, LinearSpan span2) {
            var start = Math.Max(span1.Start, span2.Start);
            var end = Math.Min(span1.End, span2.End);

            return start <= end ? FromBounds(start, end) : EmptySpan;
        }

        /// <summary>
        /// Calculates span that is an intersection of the supplied spans.
        /// </summary>
        /// <returns>Intersection or empty span if spans don't intersect</returns>
        public static LinearSpan Intersection(LinearSpan span1, int spanStart, int spanLength) {
            var start = Math.Max(span1.Start, spanStart);
            var end = Math.Min(span1.End, spanStart + spanLength);

            return start <= end ? FromBounds(start, end) : EmptySpan;
        }

        public bool Equals(LinearSpan other) {
            return Length == other.Length && Start == other.Start;
        }
    }
}
