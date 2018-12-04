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

using System.IO;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Interpreter;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.Analysis.DependencyResolution;

namespace Microsoft.PythonTools.Interpreter.Ast {
    public static class PythonModuleLoader {
        public static IPythonModule FromFile(
            IPythonInterpreter interpreter,
            string sourceFile,
            PythonLanguageVersion langVersion
        ) => FromFile(interpreter, sourceFile, langVersion, null);

        public static IPythonModule FromFile(
            IPythonInterpreter interpreter,
            string sourceFile,
            PythonLanguageVersion langVersion,
            string moduleFullName
        ) {
            using (var stream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                return FromStream(interpreter, stream, sourceFile, langVersion, moduleFullName);
            }
        }

        public static IPythonModule FromStream(
            IPythonInterpreter interpreter,
            Stream sourceFile,
            string fileName,
            PythonLanguageVersion langVersion,
            string moduleFullName
        ) {
            var sink = KeepParseErrors ? new CollectingErrorSink() : ErrorSink.Null;
            var parser = Parser.CreateParser(sourceFile, langVersion, new ParserOptions {
                StubFile = fileName.EndsWithOrdinal(".pyi", ignoreCase: true),
                ErrorSink = sink
            });
            var ast = parser.ParseFile();
            var pathResolver = interpreter is AstPythonInterpreter astPythonInterpreter ? astPythonInterpreter.CurrentPathResolver : new PathResolverSnapshot(langVersion);

            var module = new AstPythonModule(
                moduleFullName ?? ModulePath.FromFullPath(fileName, isPackage: IsPackageCheck).FullName,
                interpreter,
                ast.Documentation,
                fileName,
                (sink as CollectingErrorSink)?.Errors.Select(e => "{0} ({1}): {2}".FormatUI(fileName ?? "(builtins)", e.Span, e.Message))
            );

            module.Analyze(ast, pathResolver);
            return module;
        }

        public static IPythonModule FromTypeStub(
            IPythonInterpreter interpreter,
            string stubFile,
            PythonLanguageVersion langVersion,
            string moduleFullName
        ) => new AstCachedPythonModule(moduleFullName, stubFile);

        // Avoid hitting the filesystem, but exclude non-importable
        // paths. Ideally, we'd stop at the first path that's a known
        // search path, except we don't know search paths here.
        private static bool IsPackageCheck(string path)
            => ModulePath.IsImportable(PathUtils.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

        internal static bool KeepParseErrors = false;
    }
}
