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
using Microsoft.Python.Core;

namespace Microsoft.Python.LanguageServer.Documentation {
    internal class DocstringConverter {
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

        private Func<bool> _state;
        private readonly Stack<Func<bool>> _stateStack = new Stack<Func<bool>>();
        private int _blockIndent;
        private bool _forceFirstBlockLine;

        private readonly List<string> _lines;
        private int _lineNum = 0;

        private string CurrentLine => _lines.ElementAtOrDefault(_lineNum);
        private int CurrentIndent => CurrentLine.TakeWhile(char.IsWhiteSpace).Count();
        private string LineAt(int i) => _lines.ElementAtOrDefault(i);
        private int NextBlockIndent
            => _lines.Skip(_lineNum + 1).SkipWhile(string.IsNullOrWhiteSpace).FirstOrDefault()?.TakeWhile(char.IsWhiteSpace).Count() ?? 0;

        private DocstringConverter(string input) {
            _state = ParseText;
            _lines = SplitDocstring(input);
        }

        private string Convert() {
            while (CurrentLine != null) {
                var shouldAdvance = _state();
                if (shouldAdvance) {
                    _lineNum++;
                }
            }

            if (_state == ParseBacktickBlock || _state == ParseDoctest || _state == ParseLiteralBlock) {
                // Close out any outstanding code blocks.
                TrimOutputAndAppendLine("```");
            }

            return _builder.ToString().Trim();
        }

        private void PushAndSetState(Func<bool> next) {
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

        private bool ParseText() {
            if (string.IsNullOrWhiteSpace(CurrentLine)) {
                _state = ParseEmpty;
                return false;
            }

            if (CurrentLine.StartsWith("```")) {
                AppendLine(CurrentLine);
                PushAndSetState(ParseBacktickBlock);
                return true;
            }

            if (BeginLiteralBlock()) {
                return false;
            }

            if (BeginDoctest()) {
                return true;
            }

            if (BeginDirective()) {
                return false;
            }

            AppendTextLine(CurrentLine);
            return true;
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

        private void AppendTextLine(string line) {
            line = PreprocessTextLine(line);

            // Hack: attempt to put directives lines into their own paragraphs.
            if (!_insideInlineCode && Regex.IsMatch(line, @"^:(param|type|return|rtype|copyright|license)")) {
                AppendLine();
            }

            var parts = line.Split('`');

            for (var i = 0; i < parts.Length; i++) {
                var part = parts[i];

                if (i > 0) {
                    _insideInlineCode = !_insideInlineCode;
                    Append("`");
                }

                if (_insideInlineCode) {
                    Append(part);
                    continue;
                }

                if (i == 0) {
                    // Replace ReST style ~~~ header to prevent it being interpreted as a code block
                    // (an alternative in Markdown to triple backtick blocks).
                    if (parts.Length == 1 && Regex.IsMatch(part, @"^\s*~~~+$")) {
                        Append(part.Replace('~', '-'));
                        continue;
                    }

                    // Don't strip away asterisk-based bullet point lists.
                    var match = Regex.Match(part, @"^(\s+\*)(.*)$");
                    if (match.Success) {
                        Append(match.Groups[1].Value);
                        part = match.Groups[2].Value;
                    }
                }

                // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#hyperlink-references
                part = Regex.Replace(part, @"^_+", "");
                // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#inline-internal-targets
                part = Regex.Replace(part, @"_+$", "");

                // TODO: Strip footnote/citation references.

                // TODO: Expand

                // Escape _ and *, but ignore things like ":param \*\*kwargs:".
                part = Regex.Replace(part, @"[^\\]([_*])", @"\$1");

                Append(part);
            }

            // Go straight to the builder so that AppendLine doesn't think
            // we're actually trying to insert an extra blank line and skip
            // future whitespace.
            _builder.AppendLine();
        }

        private bool ParseEmpty() {
            if (string.IsNullOrWhiteSpace(CurrentLine)) {
                AppendLine();
                return true;
            }

            // TODO: If List-like, move into list parser, push.

            _state = ParseText;
            return false;
        }

        private bool ParseBacktickBlock() {
            if (CurrentLine.StartsWith("```")) {
                AppendLine("```");
                AppendLine();
                PopState();
                return true;
            }

            AppendLine(CurrentLine);
            return true;
        }

        private void BeginMinIndentCodeBlock(Func<bool> state) {
            AppendLine("```");
            PushAndSetState(state);
            _blockIndent = CurrentIndent;
        }

        private bool BeginDoctest() {
            if (!Regex.IsMatch(CurrentLine, @" *>>> ")) {
                return false;
            }

            BeginMinIndentCodeBlock(ParseDoctest);
            AppendLine(CurrentLine.Substring(_blockIndent));
            return true;
        }

        private bool ParseDoctest() {
            // Allow doctests like:
            // >>> ...
            //  ...
            // ...
            if (CurrentIndent < _blockIndent) {
                TrimOutputAndAppendLine("```");
                AppendLine();
                PopState();
                return false;
            }

            AppendLine(CurrentLine.Substring(_blockIndent));
            return true;
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
            // This goes to -1 so this can return false when it hits the beginning of the file.
            for (var i = _lineNum - 2; i >= -1; i--) {
                if (i < 0) {
                    return false;
                }

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

            // Special case: allow one-liners at the same indent level.
            if (CurrentIndent == 0) {
                AppendLine("```");
                PushAndSetState(ParseLiteralBlockSingleLine);
                return true;
            }

            BeginMinIndentCodeBlock(ParseLiteralBlock);
            return true;
        }

        private bool ParseLiteralBlock() {
            // Slightly different than doctest, wait until the first non-empty unindented line to exit.
            if (string.IsNullOrWhiteSpace(CurrentLine)) {
                AppendLine();
                return true;
            }

            if (CurrentIndent < _blockIndent) {
                TrimOutputAndAppendLine("```");
                AppendLine();
                PopState();
                return false;
            }

            AppendLine(CurrentLine.Substring(_blockIndent));
            return true;
        }

        private bool ParseLiteralBlockSingleLine() {
            AppendLine(CurrentLine);
            AppendLine("```");
            AppendLine();
            PopState();
            return true;
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

        private bool ParseDirective() {
            // http://docutils.sourceforge.net/docs/ref/rst/restructuredtext.html#directives

            var match = Regex.Match(CurrentLine, @"^\s*\.\.\s+(\w+)::\s*(.*)$");
            if (match.Success) {
                var directiveType = match.Groups[1].Value;
                var directive = match.Groups[2].Value;

                if (directiveType == "class") {
                    _appendDirectiveBlock = true;
                    AppendLine("```");
                    AppendLine(directive);
                    AppendLine("```");
                }
            }

            if (_blockIndent == 0) {
                // This is a one-liner directive, so pop back.
                PopState();
            } else {
                _state = ParseDirectiveBlock;
            }

            return true;
        }

        private bool ParseDirectiveBlock() {
            if (!string.IsNullOrWhiteSpace(CurrentLine) && CurrentIndent < _blockIndent) {
                PopState();
                return false;
            }

            if (_appendDirectiveBlock) {
                // This is a bit of a hack. This just trims the text and appends it
                // like top-level text, rather than doing actual indent-based recusion.
                AppendTextLine(CurrentLine.TrimStart());
            }

            return true;
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

        private void TrimOutputAndAppendLine(string line = null) {
            _builder.TrimEnd();
            _skipAppendEmptyLine = false;
            AppendLine();
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
