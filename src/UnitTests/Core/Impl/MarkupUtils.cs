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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.UnitTests.Core {
    /// <summary>
    /// To aid with testing, we define a special type of text file that can encode additional
    /// information in it.  This prevents a test writer from having to carry around multiple sources
    /// of information that must be reconstituted.  For example, instead of having to keep around the
    /// contents of a file *and* and the location of the cursor, the tester can just provide a
    /// string with the "$" character in it.  This allows for easy creation of "FIT" tests where all
    /// that needs to be provided are strings that encode every bit of state necessary in the string
    /// itself.
    /// 
    /// The current set of encoded features we support are: 
    /// 
    /// $$ - The position in the file.  There can be at most one of these.
    /// 
    /// [| ... |] - A span of text in the file.  There can be many of these and they can be nested
    /// and/or overlap the $ position.
    /// 
    /// {|Name: ... |} A span of text in the file annotated with an identifier.  There can be many of
    /// these, including ones with the same name.
    /// 
    /// Additional encoded features can be added on a case by case basis.
    /// </summary>
    public static class MarkupUtils {
        private const string PositionString = "$$";
        private const string SpanStartString = "[|";
        private const string SpanEndString = "|]";
        private const string NamedSpanStartString = "{|";
        private const string NamedSpanEndString = "|}";

        private static readonly Regex s_namedSpanStartRegex = new Regex(@"\{\| ([-_.A-Za-z0-9\+]+) \:",
            RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

        private static void Parse(
            string input, out string output, out int? position, out IDictionary<string, List<IndexSpan>> spans) {
            position = null;
            var tempSpans = new Dictionary<string, List<IndexSpan>>();

            var outputBuilder = new StringBuilder();

            var currentIndexInInput = 0;
            var inputOutputOffset = 0;

            // A stack of span starts along with their associated annotation name.  [||] spans simply
            // have empty string for their annotation name.
            var spanStartStack = new Stack<Tuple<int, string>>();

            while (true) {
                var matches = new List<(int index, string value)>();
                AddMatch(input, PositionString, currentIndexInInput, matches);
                AddMatch(input, SpanStartString, currentIndexInInput, matches);
                AddMatch(input, SpanEndString, currentIndexInInput, matches);
                AddMatch(input, NamedSpanEndString, currentIndexInInput, matches);

                var namedSpanStartMatch = s_namedSpanStartRegex.Match(input, currentIndexInInput);
                if (namedSpanStartMatch.Success) {
                    matches.Add((namedSpanStartMatch.Index, namedSpanStartMatch.Value));
                }

                if (matches.Count == 0) {
                    // No more markup to process.
                    break;
                }

                var orderedMatches = matches.OrderBy(t => t.index).ToList();
                if (orderedMatches.Count >= 2 &&
                    spanStartStack.Count > 0 &&
                    matches[0].Item1 == matches[1].Item1 - 1) {
                    // We have a slight ambiguity with cases like these:
                    //
                    // [|]    [|}
                    //
                    // Is it starting a new match, or ending an existing match.  As a workaround, we
                    // special case these and consider it ending a match if we have something on the
                    // stack already.
                    if ((matches[0].Item2 == SpanStartString && matches[1].Item2 == SpanEndString && spanStartStack.Peek().Item2 == string.Empty) ||
                        (matches[0].Item2 == SpanStartString && matches[1].Item2 == NamedSpanEndString && spanStartStack.Peek().Item2 != string.Empty)) {
                        orderedMatches.RemoveAt(0);
                    }
                }

                // Order the matches by their index
                var firstMatch = orderedMatches.First();

                var matchIndexInInput = firstMatch.Item1;
                var matchString = firstMatch.Item2;

                var matchIndexInOutput = matchIndexInInput - inputOutputOffset;
                outputBuilder.Append(input.Substring(currentIndexInInput, matchIndexInInput - currentIndexInInput));

                currentIndexInInput = matchIndexInInput + matchString.Length;
                inputOutputOffset += matchString.Length;

                switch (matchString.Substring(0, 2)) {
                    case PositionString:
                        if (position.HasValue) {
                            throw new ArgumentException(string.Format("Saw multiple occurrences of {0}", PositionString));
                        }

                        position = matchIndexInOutput;
                        break;

                    case SpanStartString:
                        spanStartStack.Push(Tuple.Create(matchIndexInOutput, string.Empty));
                        break;

                    case SpanEndString:
                        if (spanStartStack.Count == 0) {
                            throw new ArgumentException(string.Format("Saw {0} without matching {1}", SpanEndString, SpanStartString));
                        }

                        if (spanStartStack.Peek().Item2.Length > 0) {
                            throw new ArgumentException(string.Format("Saw {0} without matching {1}", NamedSpanStartString, NamedSpanEndString));
                        }

                        PopSpan(spanStartStack, tempSpans, matchIndexInOutput);
                        break;

                    case NamedSpanStartString:
                        var name = namedSpanStartMatch.Groups[1].Value;
                        spanStartStack.Push(Tuple.Create(matchIndexInOutput, name));
                        break;

                    case NamedSpanEndString:
                        if (spanStartStack.Count == 0) {
                            throw new ArgumentException(string.Format("Saw {0} without matching {1}", NamedSpanEndString, NamedSpanStartString));
                        }

                        if (spanStartStack.Peek().Item2.Length == 0) {
                            throw new ArgumentException(string.Format("Saw {0} without matching {1}", SpanStartString, SpanEndString));
                        }

                        PopSpan(spanStartStack, tempSpans, matchIndexInOutput);
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }

            if (spanStartStack.Count > 0) {
                throw new ArgumentException(string.Format("Saw {0} without matching {1}", SpanStartString, SpanEndString));
            }

            // Append the remainder of the string.
            outputBuilder.Append(input.Substring(currentIndexInInput));
            output = outputBuilder.ToString();
            spans = tempSpans.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static V GetOrAdd<K, V>(IDictionary<K, V> dictionary, K key, Func<K, V> function) {
            if (!dictionary.TryGetValue(key, out var value)) {
                value = function(key);
                dictionary.Add(key, value);
            }

            return value;
        }

        private static void PopSpan(
            Stack<Tuple<int, string>> spanStartStack,
            IDictionary<string, List<IndexSpan>> spans,
            int finalIndex) {
            var spanStartTuple = spanStartStack.Pop();

            var span = IndexSpan.FromBounds(spanStartTuple.Item1, finalIndex);
            GetOrAdd(spans, spanStartTuple.Item2, _ => new List<IndexSpan>()).Add(span);
        }

        private static void AddMatch(string input, string value, int currentIndex, List<(int index, string value)> matches) {
            var index = input.IndexOf(value, currentIndex, StringComparison.Ordinal);
            if (index >= 0) {
                matches.Add((index, value));
            }
        }

        private static void GetPositionAndSpans(
            string input, out string output, out int? cursorPositionOpt, out List<IndexSpan> spans) {
            Parse(input, out output, out cursorPositionOpt, out var dictionary);

            spans = GetOrAdd(dictionary, string.Empty, _ => new List<IndexSpan>());
        }

        public static void GetPositionAndNamedSpans(
            string input, out string output, out int? cursorPositionOpt, out IDictionary<string, List<IndexSpan>> spans) {
            Parse(input, out output, out cursorPositionOpt, out spans);
        }

        public static void GetNamedSpans(string input, out string output, out IDictionary<string, List<IndexSpan>> spans)
            => GetPositionAndNamedSpans(input, out output, out var _, out spans);

        public static void GetPositionAndSpans(string input, out string output, out int cursorPosition, out List<IndexSpan> spans) {
            GetPositionAndSpans(input, out output, out int? pos, out spans);
            cursorPosition = pos.Value;
        }

        public static void GetPosition(string input, out string output, out int? cursorPosition)
            => GetPositionAndSpans(input, out output, out cursorPosition, out List<IndexSpan> _);

        public static void GetPosition(string input, out string output, out int cursorPosition)
            => GetPositionAndSpans(input, out output, out cursorPosition, out var _);

        public static void GetPositionAndSpan(string input, out string output, out int? cursorPosition, out IndexSpan? textSpan) {
            GetPositionAndSpans(input, out output, out cursorPosition, out List<IndexSpan> spans);
            textSpan = spans.Count == 0 ? null : (IndexSpan?)spans.Single();
        }

        public static void GetPositionAndSpan(string input, out string output, out int cursorPosition, out IndexSpan textSpan) {
            GetPositionAndSpans(input, out output, out cursorPosition, out var spans);
            textSpan = spans.Single();
        }

        public static void GetSpans(string input, out string output, out List<IndexSpan> spans) {
            GetPositionAndSpans(input, out output, out int? pos, out spans);
        }

        public static void GetSpan(string input, out string output, out IndexSpan textSpan) {
            GetSpans(input, out output, out List<IndexSpan> spans);
            textSpan = spans.Single();
        }
    }
}
