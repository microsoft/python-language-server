#!/usr/bin/env python

import argparse
import contextlib
from collections import defaultdict
import string
import sys
import os.path


def main():
    script_path = os.path.realpath(__file__)
    script_dir = os.path.dirname(script_path)
    default_input = os.path.join(
        script_dir, "UnitTests", "TestData", "gen", "completion"
    )
    default_output = os.path.join(
        script_dir, "LanguageServer", "Test", "GenTests.cs"
    )

    parser = argparse.ArgumentParser(
        description="Generate completion and hover tests",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument(
        "--ignore",
        type=str,
        help="comma separated list of tests to disable, of the form <filename>(:<linenum>)",
    )
    parser.add_argument(
        "--only", type=str, help="comma separated list of tests to generate"
    )
    parser.add_argument(
        "-o",
        "--out",
        nargs="?",
        type=argparse.FileType("w"),
        default=default_output,
        help="output file",
    )
    parser.add_argument(
        "-i",
        "--input",
        type=str,
        default=default_input,
        help="location of completions directory",
    )
    args = parser.parse_args()

    if args.only:
        to_generate = set(args.only.split(","))
    else:
        to_generate = set(DEFAULT_TEST_FILES)

    line_skip = defaultdict(set)

    if args.ignore:
        for i in args.ignore.split(","):
            if ":" not in i:
                to_generate.discard(i)
            else:
                name, line = i.split(":")

                try:
                    line = int(line)
                except:
                    print(f"error in format of ignored item {i}", file=sys.stderr)
                    return

                line_skip[name].add(line)

    to_generate = sorted(to_generate)

    with contextlib.redirect_stdout(args.out):
        print(PREAMBLE)

        for name in to_generate:
            filename = os.path.join(args.input, name + ".py")
            ignored_lines = line_skip[name]
            create_tests(name, filename, ignored_lines)

        print(POSTAMBLE)


def create_tests(name, filename, ignored_lines):
    camel_name = snake_to_camel(name)

    with open(filename) as fp:
        lines = fp.read().splitlines()

    width = len(str(len(lines)))

    tests = []

    for i, line in enumerate(lines):
        if i in ignored_lines:
            continue

        line: str = line.strip()
        if not line.startswith("#?"):
            continue

        line = line[2:].strip()

        next_line = lines[i + 1]
        col = len(next_line)

        if " " in line:
            maybe_num = line.split(" ", 1)

            try:
                col = int(maybe_num[0])
                line = maybe_num[1]
            except ValueError:
                pass

        filt = next_line[:col].lstrip()
        filt = select_filter(filt, ". {[(")

        args = line.strip()
        func_name = "Line_{0:0{pad}}".format(i + 1, pad=width)
        func_name = camel_name + "_" + func_name

        tmpl = COMPLETION_TEST if args.startswith("[") else HOVER_TEST
        tests.append(
            tmpl.format(
                name=func_name,
                module=csharp_str(name),
                line=i + 1,
                col=col,
                args=csharp_str(args),
                filter=csharp_str(filt),
            )
        )

    if tests:
        print(CLASS_PREAMBLE.format(name=camel_name))
        for t in tests:
            print(t)
        print(CLASS_POSTAMBLE)


DEFAULT_TEST_FILES = [
    "arrays",
    "async_",
    "basic",
    "classes",
    "completion",
    "complex",
    "comprehensions",
    "context",
    "decorators",
    "definition",
    "descriptors",
    "docstring",
    "dynamic_arrays",
    "dynamic_params",
    "flow_analysis",
    "fstring",
    "functions",
    "generators",
    "imports",
    "invalid",
    "isinstance",
    "keywords",
    "lambdas",
    "named_param",
    "on_import",
    "ordering",
    "parser",
    "pep0484_basic",
    "pep0484_comments",
    "pep0484_typing",
    "pep0526_variables",
    "precedence",
    "recursion",
    "stdlib",
    "sys_path",
    "types",
]

PREAMBLE = """// Python Tools for Visual Studio
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace GenTests {"""

POSTAMBLE = """
    public class GenTest : LanguageServerTestBase {
        private static readonly Dictionary<string, Task<IDocumentAnalysis>> _analysis = new Dictionary<string, Task<IDocumentAnalysis>>();

        private static readonly InterpreterConfiguration _interpreter = PythonVersions.LatestAvailable3X;
        private static readonly PythonLanguageVersion _version = _interpreter.Version.ToLanguageVersion();

        private static readonly CompletionSource _cs = new CompletionSource(new PlainTextDocumentationSource(), ServerSettings.completion);
        private static readonly HoverSource _hs = new HoverSource(new PlainTextDocumentationSource());

        static GenTest() {
            _interpreter.TypeshedPath = TestData.GetDefaultTypeshedPath();
        }

        protected async Task<IDocumentAnalysis> GetGenAnalysisAsync(string module) {
            // Create an analyzer per module. This is slower than creating a single
            // analyzer shared between all GenTest instances, but sharing them makes
            // the "types" module fail (due to a bug where the name "types" shadows
            // a builtin module name).
            if (_analysis.TryGetValue(module, out var analysis)) {
                return await analysis;
            }

            var root = TestData.GetPath("TestData", "gen", "completion");

            var sm = CreateServiceManager();
            sm.AddService(new PythonAnalyzer(sm));
            sm.AddService(await PythonInterpreter.CreateAsync(_interpreter, root, sm));
            sm.AddService(new RunningDocumentTable(sm));

            var src = TestData.GetPath("TestData", "gen", "completion", module + ".py");
            analysis = GetAnalysisAsync(File.ReadAllText(src), sm, modulePath: src);
            _analysis[module] = analysis;

            return await analysis;
        }

        protected async Task DoCompletionTest(string module, int lineNum, int col, string args, string filter) {
            var tests = string.IsNullOrWhiteSpace(args) ? new List<string>() : ParseStringList(args);

            var analysis = await GetGenAnalysisAsync(module);

            var res = _cs.GetCompletions(analysis, new SourceLocation(lineNum + 1, col + 1));
            res.Should().NotBeNull();

            var items = res.Completions?.Select(item => item.insertText).Where(t => t.Contains(filter)).ToList() ?? new List<string>();

            if (tests.Count == 0) {
                items.Should().BeEmpty();
            } else {
                items.Should().Contain(tests);
            }
        }

        protected async Task DoHoverTest(string module, int lineNum, int col, string args) {
            var tests = string.IsNullOrWhiteSpace(args)
                ? new List<string>()
                : args.Split(' ', options: StringSplitOptions.RemoveEmptyEntries).Select(s => s.EndsWith("()") ? s.Substring(0, s.Length - 2) : s).ToList();

            var analysis = await GetGenAnalysisAsync(module);

            var res = _hs.GetHover(analysis, new SourceLocation(lineNum + 1, col + 1));
            res.Should().NotBeNull();

            if (tests.Count == 0) {
                res.contents.value.Should().BeEmpty();
            } else {
                res.contents.value.Should().ContainAll(tests);
            }
        }

        protected List<string> ParseStringList(string s) {
            var list = new List<string>();

            using (var reader = new StringReader(s)) {
                var tokenizer = new Tokenizer(_version);
                tokenizer.Initialize(reader);

                while (!tokenizer.IsEndOfFile) {
                    var token = tokenizer.GetNextToken();

                    if (token.Kind == TokenKind.EndOfFile) {
                        break;
                    }

                    switch (token.Kind) {
                        case TokenKind.Constant when token != Tokens.NoneToken && (token.Value is string || token.Value is AsciiString):
                            list.Add(token.Image);
                            break;
                    }
                }
            }

            return list;
        }
    }
}
"""

CLASS_PREAMBLE = """    [TestClass]
    public class {name}Tests : GenTest {{
        public TestContext TestContext {{ get; set; }}

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{{TestContext.FullyQualifiedTestClassName}}.{{TestContext.TestName}}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();"""

CLASS_POSTAMBLE = """
    }"""

COMPLETION_TEST = """
        [TestMethod, Priority(0)] public async Task {name}_Completion() => await DoCompletionTest({module}, {line}, {col}, {args}, {filter});"""


HOVER_TEST = """
        [TestMethod, Priority(0)] public async Task {name}_Hover() => await DoHoverTest({module}, {line}, {col}, {args});"""


def snake_to_camel(s):
    return string.capwords(s, "_").replace("_", "")


def select_filter(s, cs):
    found = False
    for c in cs:
        i = s.rfind(c)
        if i != -1:
            found = True
            s = s[i + 1 :]

    if found:
        return s
    return ""


def csharp_str(s):
    if s is None:
        return "null"

    s = s.replace('"', '""')
    return '@"{}"'.format(s)


if __name__ == "__main__":
    main()

