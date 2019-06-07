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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using FluentAssertions;

namespace TestUtilities {
    [ExcludeFromCodeCoverage]
    public static class Baseline {
        public static string CompareStrings(string expected, string actual) {
            var result = new StringBuilder();

            var length = Math.Min(expected.Length, actual.Length);
            for (var i = 0; i < length; i++) {
                if (expected[i] != actual[i]) {
                    result.AppendLine(FormattableString.Invariant($"Position: {i}: expected: '{expected[i]}', actual '{actual[i]}'"));
                    if (i > 6 && i < length - 6) {
                        result.Append(FormattableString.Invariant($"Context: {expected.Substring(i - 6, 12)} -> {actual.Substring(i - 6, 12)}"));
                    }
                    break;
                }

            }

            if (expected.Length != actual.Length) {
                result.Append(FormattableString.Invariant($"\r\nLength different. Expected: '{expected.Length}' , actual '{actual.Length}'"));
            }

            return result.ToString();
        }

        public static void CompareStringLines(string expected, string actual) {
            var line = CompareLines(expected, actual, out var baseLine, out var actualLine, out var index);
            line.Should().Be(0, $@"there should be no difference at line {line}
  Expected:{baseLine.Trim()}
  Actual:{actualLine.Trim()}
  Difference at position {index}{Environment.NewLine}");
        }

        public static int CompareLines(string expected, string actual, out string expectedLine, out string actualLine, out int index, bool ignoreCase = false) {
            var actualReader = new StringReader(actual);
            var expectedReader = new StringReader(expected);

            var lineNum = 1;
            index = 0;

            for (; ; lineNum++) {
                expectedLine = expectedReader.ReadLine();
                actualLine = actualReader.ReadLine();

                if (expectedLine == null || actualLine == null) {
                    break;
                }

                var minLength = Math.Min(expectedLine.Length, actualLine.Length);
                for (var i = 0; i < minLength; i++) {
                    var act = actualLine[i];
                    var exp = expectedLine[i];

                    if (ignoreCase) {
                        act = char.ToLowerInvariant(act);
                        exp = char.ToLowerInvariant(exp);
                    }

                    if (act != exp) {
                        index = i + 1;
                        return lineNum;
                    }
                }

                if (expectedLine.Length != actualLine.Length) {
                    index = minLength + 1;
                    return lineNum;
                }
            }

            if (expectedLine == null && actualLine == null) {
                expectedLine = string.Empty;
                actualLine = string.Empty;

                return 0;
            }

            return lineNum;
        }

        public static void CompareToFile(string baselineFile, string actual, bool regenerateBaseline = false, bool ignoreCase = false) {
            if (regenerateBaseline) {
                if (File.Exists(baselineFile)) {
                    File.SetAttributes(baselineFile, FileAttributes.Normal);
                }

                File.WriteAllText(baselineFile, actual.Trim());
                return;
            }

            actual = actual.Trim();
            var expected = File.ReadAllText(baselineFile).Trim();
            var line = CompareLines(expected, actual, out var baseLine, out var actualLine, out var index, ignoreCase);
            line.Should().Be(0,
                $@"there should be no difference at line {line}
  Expected:{baseLine.Trim()}
  Actual:{actualLine.Trim()}
  BaselineFile:{Path.GetFileName(baselineFile)}
  Difference at {index}{Environment.NewLine}"
            );
        }
    }
}
