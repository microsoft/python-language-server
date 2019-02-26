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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Python.Core;

namespace Microsoft.Python.LanguageServer.Documentation {
    internal class DocstringConverter {
        private static readonly string[] PotentialHeaders = new[] { "=", "-", "~", "+" };

        /// <summary>
        /// Converts a docstring to a plaintext, human readable form. This will
        /// first strip any common leading indention (like inspect.cleandoc),
        /// then remove duplicate empty/whitespace lines.
        /// </summary>
        /// <param name="docstring">The docstring to convert, likely from the AST.</param>
        /// <returns>The converted docstring, with Environment.NewLine line endings.</returns>
        public static string ToPlaintext(string docstring) {
            var lines = SplitDocstring(docstring);
            var output = new List<string>();

            foreach (var line in lines) {
                if (string.IsNullOrWhiteSpace(line) && string.IsNullOrWhiteSpace(output.LastOrDefault())) {
                    continue;
                }
                output.Add(line);
            }

            return string.Join(Environment.NewLine, output).TrimEnd();
        }

        /// <summary>
        /// Converts a docstring to a markdown format. This does various things,
        /// including removing common indention, escaping characters, handling
        /// code blocks, and more.
        /// </summary>
        /// <param name="docstring">The docstring to convert, likely from the AST.</param>
        /// <returns>The converted docstring, with Environment.NewLine line endings.</returns>
        public static string ToMarkdown(string docstring) => new DocstringConverter(docstring).Convert();

        private readonly StringBuilder _builder = new StringBuilder();
        private bool _skipAppendEmptyLine = true;
        private bool _insideInlineCode = false;
        private bool _appendDirectiveBlock = false;

        private Action _state;
        private readonly Stack<Action> _stateStack = new Stack<Action>();

        private readonly IReadOnlyList<string> _lines;
        private int _lineNum = 0;
        private void EatLine() => _lineNum++;

        private string CurrentLine => _lines.ElementAtOrDefault(_lineNum);
        private int CurrentIndent => CountLeadingSpaces(CurrentLine);
        private string LineAt(int i) => _lines.ElementAtOrDefault(i);
        private int NextBlockIndent
            => _lines.Skip(_lineNum + 1).SkipWhile(string.IsNullOrWhiteSpace)
            .FirstOrDefault()?.TakeWhile(char.IsWhiteSpace).Count() ?? 0;

        private int _blockIndent = 0;
        private bool CurrentLineIsOutsideBlock => CurrentIndent < _blockIndent;
        private string CurrentLineWithinBlock => CurrentLine.Substring(_blockIndent);

        private DocstringConverter(string input) {
            _state = ParseText;
            _lines = SplitDocstring(input);
        }

        private string Convert() {
            while (CurrentLine != null) {
                var before = _state;
                var beforeLine = _lineNum;

                _state();

                // Parser must make progress; either the state or line number must change.
                if (_state == before && _lineNum == beforeLine) {
                    Debug.Fail("Infinite loop during docstring conversion");
                    break;
                }
            }

            // Close out any outstanding code blocks.
            if (_state == ParseBacktickBlock || _state == ParseDoctest || _state == ParseLiteralBlock) {
                TrimOutputAndAppendLine("```");
            } else if (_insideInlineCode) {
                TrimOutputAndAppendLine("`", true);
            }

            return _builder.ToString().Trim();
        }

        private void PushAndSetState(Action next) {
            if (_state == ParseText) {
                _insideInlineCode = false;
            }

            _stateStack.Push(_state);
            _state = next;
        }

        private void PopState() {
            _state = _stateStack.Pop();

            if (_state == ParseText) {
                // Terminate inline code when leaving a block.
                _insideInlineCode = false;
            }
        }

        private void ParseText() {
            if (string.IsNullOrWhiteSpace(CurrentLine)) {
                _state = ParseEmpty;
                return;
            }

            if (BeginBacktickBlock()) {
                return;
            }

            if (BeginLiteralBlock()) {
                return;
            }

            if (BeginDoctest()) {
                return;
            }

            if (BeginDirective()) {
                return;
            }

            // TODO: Push into Google/Numpy style list parser.

            AppendTextLine(CurrentLine);
            EatLine();
        }

        private void AppendTextLine(string line) {
            line = PreprocessTextLine(line);

            // Hack: attempt to put directives lines into their own paragraphs.
            // This should be removed once proper list-like parsing is written.
            if (!_insideInlineCode && Regex.IsMatch(line, @"^\s*:(param|arg|type|return|rtype|raise|except|var|ivar|cvar|copyright|license)")) {
                AppendLine();
            }

            var parts = line.Split('`');

            for (var i = 0; i < parts.Length; i++) {
                var part = parts[i];

                if (i > 0) {
                    _insideInlineCode = !_insideInlineCode;
                    Append('`');
                }

                if (_insideInlineCode) {
                    Append(part);
                    continue;
                }

                if (i == 0) {
                    // Only one part, and not inside code, so check header cases.
                    if (parts.Length == 1) {
                        // Handle weird separator lines which contain random spaces.
                        foreach (var h in PotentialHeaders) {
                            var hEsc = Regex.Escape(h);
                            if (Regex.IsMatch(part, $"^\\s*{hEsc}+(\\s+{hEsc}+)+$")) {
                                part = Regex.Replace(part, @"\s", h);
                                break;
                            }
                        }

                        // Replace ReST style ~~~ header to prevent it being interpreted as a code block
                        // (an alternative in Markdown to triple backtick blocks).
                        if (Regex.IsMatch(part, @"^\s*~~~+$")) {
                            Append(part.Replace('~', '-'));
                            continue;
                        }

                        // Replace +++ heading too.
                        // TODO: Handle the rest of these, and the precedence order (which depends on the
                        // order heading lines are seen, not what the line contains).
                        // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#sections
                        if (Regex.IsMatch(part, @"^\s*\+\+\++$")) {
                            Append(part.Replace('+', '-'));
                            continue;
                        }
                    }

                    // Don't strip away asterisk-based bullet point lists.
                    //
                    // TODO: Replace this with real list parsing. This may have
                    // false positives and cause random italics when the ReST list
                    // doesn't match Markdown's specification.
                    var match = Regex.Match(part, @"^(\s+\* )(.*)$");
                    if (match.Success) {
                        Append(match.Groups[1].Value);
                        part = match.Groups[2].Value;
                    }
                }

                // TODO: Find a better way to handle this; the below breaks escaped
                // characters which appear at the beginning or end of a line.
                // Applying this only when i == 0 or i == parts.Length-1 may work.

                // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#hyperlink-references
                // part = Regex.Replace(part, @"^_+", "");
                // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#inline-internal-targets
                // part = Regex.Replace(part, @"_+$", "");

                // TODO: Strip footnote/citation references.

                // Escape _, *, and ~, but ignore things like ":param \*\*kwargs:".
                part = Regex.Replace(part, @"(?<!\\)([_*~])", @"\$1");

                Append(part);
            }

            // Go straight to the builder so that AppendLine doesn't think
            // we're actually trying to insert an extra blank line and skip
            // future whitespace. Empty line deduplication is already handled
            // because Append is used above.
            _builder.AppendLine();
        }

        private string PreprocessTextLine(string line) {
            // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#literal-blocks
            if (Regex.IsMatch(line, @"^\s*::$")) {
                return string.Empty;
            }
            line = Regex.Replace(line, @"\s+::$", "");
            line = Regex.Replace(line, @"(\S)\s*::$", "$1:");

            // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#interpreted-text
            line = Regex.Replace(line, @":[\w_\-+:.]+:`", "`");
            line = Regex.Replace(line, @"`:[\w_\-+:.]+:", "`");

            line = line.Replace("``", "`");
            return line;
        }

        private void ParseEmpty() {
            if (string.IsNullOrWhiteSpace(CurrentLine)) {
                AppendLine();
                EatLine();
                return;
            }

            _state = ParseText;
        }

        private void BeginMinIndentCodeBlock(Action state) {
            AppendLine("```");
            PushAndSetState(state);
            _blockIndent = CurrentIndent;
        }

        private bool BeginBacktickBlock() {
            if (CurrentLine.StartsWith("```")) {
                AppendLine(CurrentLine);
                PushAndSetState(ParseBacktickBlock);
                EatLine();
                return true;
            }
            return false;
        }

        private void ParseBacktickBlock() {
            if (CurrentLine.StartsWith("```")) {
                AppendLine("```");
                AppendLine();
                PopState();
            } else {
                AppendLine(CurrentLine);
            }

            EatLine();
        }

        private bool BeginDoctest() {
            if (!Regex.IsMatch(CurrentLine, @" *>>> ")) {
                return false;
            }

            BeginMinIndentCodeBlock(ParseDoctest);
            AppendLine(CurrentLineWithinBlock);
            EatLine();
            return true;
        }

        private void ParseDoctest() {
            if (CurrentLineIsOutsideBlock || string.IsNullOrWhiteSpace(CurrentLine)) {
                TrimOutputAndAppendLine("```");
                AppendLine();
                PopState();
                return;
            }

            AppendLine(CurrentLineWithinBlock);
            EatLine();
        }

        private bool BeginLiteralBlock() {
            // The previous line must be empty.
            var prev = LineAt(_lineNum - 1);
            if (prev == null) {
                return false;
            } else if (!string.IsNullOrWhiteSpace(prev)) {
                return false;
            }

            // Find the previous paragraph and check that it ends with ::
            var i = _lineNum - 2;
            for (; i >= 0; i--) {
                var line = LineAt(i);

                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                // Safe to ignore whitespace after the :: because all lines have been TrimEnd'd.
                if (line.EndsWith("::")) {
                    break;
                }

                return false;
            }

            if (i < 0) {
                return false;
            }

            // Special case: allow one-liners at the same indent level.
            if (CurrentIndent == 0) {
                AppendLine("```");
                PushAndSetState(ParseLiteralBlockSingleLine);
                return true;
            }

            BeginMinIndentCodeBlock(ParseLiteralBlock);
            return true;
        }

        private void ParseLiteralBlock() {
            // Slightly different than doctest, wait until the first non-empty unindented line to exit.
            if (string.IsNullOrWhiteSpace(CurrentLine)) {
                AppendLine();
                EatLine();
                return;
            }

            if (CurrentLineIsOutsideBlock) {
                TrimOutputAndAppendLine("```");
                AppendLine();
                PopState();
                return;
            }

            AppendLine(CurrentLineWithinBlock);
            EatLine();
        }

        private void ParseLiteralBlockSingleLine() {
            AppendLine(CurrentLine);
            AppendLine("```");
            AppendLine();
            PopState();
            EatLine();
        }

        private bool BeginDirective() {
            if (!Regex.IsMatch(CurrentLine, @"^\s*\.\. ")) {
                return false;
            }

            PushAndSetState(ParseDirective);
            _blockIndent = NextBlockIndent;
            _appendDirectiveBlock = false;
            return true;
        }

        private void ParseDirective() {
            // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#directives

            var match = Regex.Match(CurrentLine, @"^\s*\.\.\s+(\w+)::\s*(.*)$");
            if (match.Success) {
                var directiveType = match.Groups[1].Value;
                var directive = match.Groups[2].Value;

                if (directiveType == "class") {
                    _appendDirectiveBlock = true;
                    AppendLine();
                    AppendLine("```");
                    AppendLine(directive);
                    AppendLine("```");
                    AppendLine();
                }
            }

            if (_blockIndent == 0) {
                // This is a one-liner directive, so pop back.
                PopState();
            } else {
                _state = ParseDirectiveBlock;
            }

            EatLine();
        }

        private void ParseDirectiveBlock() {
            if (!string.IsNullOrWhiteSpace(CurrentLine) && CurrentLineIsOutsideBlock) {
                PopState();
                return;
            }

            if (_appendDirectiveBlock) {
                // This is a bit of a hack. This just trims the text and appends it
                // like top-level text, rather than doing actual indent-based recursion.
                AppendTextLine(CurrentLine.TrimStart());
            }

            EatLine();
        }

        private void AppendLine(string line = null) {
            if (!string.IsNullOrWhiteSpace(line)) {
                _builder.AppendLine(line);
                _skipAppendEmptyLine = false;
            } else if (!_skipAppendEmptyLine) {
                _builder.AppendLine();
                _skipAppendEmptyLine = true;
            }
        }

        private void Append(string text) {
            _builder.Append(text);
            _skipAppendEmptyLine = false;
        }

        private void Append(char c) {
            _builder.Append(c);
            _skipAppendEmptyLine = false;
        }

        private void TrimOutputAndAppendLine(string line = null, bool noNewLine = false) {
            _builder.TrimEnd();
            _skipAppendEmptyLine = false;

            if (!noNewLine) {
                AppendLine();
            }

            AppendLine(line);
        }

        private static List<string> SplitDocstring(string docstring) {
            // As done by inspect.cleandoc.
            docstring = docstring.Replace("\t", "        ");

            var lines = docstring.SplitLines()
                .Select(s => s.TrimEnd())
                .ToList();

            if (lines.Count > 0) {
                var first = lines[0].TrimStart();
                if (first == string.Empty) {
                    first = null;
                } else {
                    lines.RemoveAt(0);
                }

                lines = StripLeadingWhiteSpace(lines);

                if (first != null) {
                    lines.Insert(0, first);
                }
            }

            return lines;
        }

        private static List<string> StripLeadingWhiteSpace(List<string> lines, int? trim = null) {
            var amount = trim ?? LargestTrim(lines);
            return lines.Select(line => amount > line.Length ? string.Empty : line.Substring(amount)).ToList();
        }

        private static int LargestTrim(IEnumerable<string> lines) => lines.Where(s => !string.IsNullOrWhiteSpace(s)).Select(CountLeadingSpaces).DefaultIfEmpty().Min();

        private static int CountLeadingSpaces(string s) => s.TakeWhile(char.IsWhiteSpace).Count();
    }
}
