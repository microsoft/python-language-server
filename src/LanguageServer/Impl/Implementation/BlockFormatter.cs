using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.Python.LanguageServer.Implementation {
    /// <summary>
    /// Port of BlockFormatProviders in vscode-python.
    /// </summary>
    internal class BlockFormatter {
        private static readonly Regex IfRegex = CompileRegex(@"^( |\t)*if +.*: *$");
        private static readonly Regex ElIfRegex = CompileRegex(@"^( |\t)*elif +.*: *$");
        private static readonly Regex ElseRegex = CompileRegex(@"^( |\t)*else *: *$");
        private static readonly Regex ForInRegex = CompileRegex(@"^( |\t)*for \w in .*: *$");
        private static readonly Regex AsyncForInRegex = CompileRegex(@"^( |\t)*async *for \w in .*: *$");
        private static readonly Regex WhileRegex = CompileRegex(@"^( |\t)*while .*: *$");
        private static readonly Regex TryRegex = CompileRegex(@"^( |\t)*try *: *$");
        private static readonly Regex FinallyRegex = CompileRegex(@"^( |\t)*finally *: *$");
        private static readonly Regex ExceptRegex = CompileRegex(@"^( |\t)*except *\w* *(as)? *\w* *: *$");
        private static readonly Regex DefRegex = CompileRegex(@"^( |\t)*def \w *\(.*$");
        private static readonly Regex AsyncDefRegex = CompileRegex(@"^( |\t)*async *def \w *\(.*$");
        private static readonly Regex ClassRegex = CompileRegex(@"^( |\t)*class *\w* *.*: *$");

        private static readonly IEnumerable<Regex> BoundaryBlocks = new[] { DefRegex, AsyncDefRegex, ClassRegex };

        private static readonly IEnumerable<BlockFormatter> Formatters = new[] {
            new BlockFormatter(ElseRegex, new[] { IfRegex, ElIfRegex, ForInRegex, AsyncForInRegex, WhileRegex, TryRegex, ExceptRegex }),
            new BlockFormatter(ElIfRegex, new[] { IfRegex, ElIfRegex }),
            new BlockFormatter(ExceptRegex, new[] { TryRegex, ExceptRegex }),
            new BlockFormatter(ExceptRegex, new[] { TryRegex, ExceptRegex }),
            new BlockFormatter(FinallyRegex, new[] { TryRegex, ExceptRegex }),
        };

        public static async Task<TextEdit[]> ProvideEdits(TextReader reader, Position position, FormattingOptions options) {
            Check.ArgumentOutOfRange(nameof(position), () => position.line < 0);

            if (position.line == 0) {
                return Array.Empty<TextEdit>();
            }

            var lines = new List<string>(position.line + 1);

            for (var i = 0; i <= position.line; i++) {
                var curr = await reader.ReadLineAsync();

                if (curr == null) {
                    throw new ArgumentException($"reached end of file before {nameof(position)}", nameof(position));
                }

                lines.Add(curr);
            }

            var line = lines[position.line];

            var formatter = Formatters.FirstOrDefault(f => f.CanProvideEdits(line));
            if (formatter == null) {
                return Array.Empty<TextEdit>();
            }

            return formatter.ProvideEdits(lines, position, options);
        }

        private readonly Regex _blockRegexp;
        private readonly IEnumerable<Regex> _previousBlockRegexps;

        private BlockFormatter(Regex blockRegexp, IEnumerable<Regex> previousBlockRegexps) {
            Check.ArgumentNull(nameof(blockRegexp), blockRegexp);
            Check.Argument(nameof(previousBlockRegexps), () => !previousBlockRegexps.IsNullOrEmpty());

            _blockRegexp = blockRegexp;
            _previousBlockRegexps = previousBlockRegexps;
        }

        private bool CanProvideEdits(string line) => _blockRegexp.IsMatch(line);

        private TextEdit[] ProvideEdits(IList<string> lines, Position position, FormattingOptions options) {
            var line = lines[position.line];
            var lineFirstNonWhitespace = FirstNonWhitespaceCharacterIndex(line);

            // We can have else for the following blocks:
            // if:
            // elif x:
            // for x in y:
            // while x:

            // We need to find a block statement that is less than or equal to this statement block (but not greater)
            for (var lineNum = position.line - 1; lineNum >= 0; lineNum--) {
                var prevLine = lines[lineNum];

                // Oops, we've reached a boundary (like the function or class definition)
                // Get out of here
                if (BoundaryBlocks.Any(regexp => regexp.IsMatch(prevLine))) {
                    return Array.Empty<TextEdit>();
                }

                var blockRegex = _previousBlockRegexps.FirstOrDefault(regex => regex.IsMatch(prevLine));
                if (blockRegex == null) {
                    continue;
                }

                var startOfBlockInLine = FirstNonWhitespaceCharacterIndex(prevLine);
                if (startOfBlockInLine > lineFirstNonWhitespace) {
                    continue;
                }

                var startPosition = new Position { line = position.line, character = 0 };
                var endPosition = new Position { line = position.line, character = lineFirstNonWhitespace - startOfBlockInLine };

                if (endPosition.character == 0) {
                    // current block cannot be at the same level as a preivous block
                    continue;
                }

                if (options.insertSpaces) {
                    return new[] { new TextEdit {
                        range = new Range {start = startPosition, end = endPosition }
                    } };
                }

                var prefixOfPreviousBlock = prevLine.Substring(0, startOfBlockInLine);

                return new[] {
                    new TextEdit {
                        range = new Range {
                            start = startPosition,
                            end = new Position {line = position.line, character = lineFirstNonWhitespace }
                        },
                        newText = prefixOfPreviousBlock
                    }
                };
            }

            return Array.Empty<TextEdit>();
        }

        private int FirstNonWhitespaceCharacterIndex(string s) => s.TakeWhile(c => char.IsWhiteSpace(c)).Count();

        private static Regex CompileRegex(string pattern) => new Regex(pattern, RegexOptions.Compiled);
    }
}
