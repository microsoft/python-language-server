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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    public abstract class PythonModuleType : IPythonType, IPythonFile {
        protected IDictionary<string, IMember> Members { get; set; } = new Dictionary<string, IMember>();
        protected ILogger Log => Interpreter.Log;

        protected PythonModuleType(string name) {
            Check.ArgumentNotNull(nameof(name), name);
            Name = name;
        }

        protected PythonModuleType(string name, IPythonInterpreter interpreter)
            : this(name) {
            Check.ArgumentNotNull(nameof(interpreter), interpreter);
            Interpreter = interpreter;
        }

        protected PythonModuleType(string name, string filePath, Uri uri, IPythonInterpreter interpreter)
            : this(name, interpreter) {
            if (uri == null && !string.IsNullOrEmpty(filePath)) {
                Uri.TryCreate(filePath, UriKind.Absolute, out uri);
            }
            Uri = uri;
            FilePath = filePath ?? uri?.LocalPath;
        }

        #region IPythonType
        public string Name { get; }
        public virtual string Documentation { get; } = string.Empty;

        public virtual IPythonModule DeclaringModule => null;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltin => true;
        public bool IsTypeFactory => false;
        public IPythonFunction GetConstructor() => null;
        public PythonMemberType MemberType => PythonMemberType.Module;
        #endregion

        #region IMemberContainer
        public abstract IMember GetMember(string name);
        public abstract IEnumerable<string> GetMemberNames();
        #endregion

        #region IPythonFile

        public virtual string FilePath { get; }
        public virtual Uri Uri { get; }
        public virtual IPythonInterpreter Interpreter { get; }
        #endregion

        public IEnumerable<string> ParseErrors { get; private set; } = Enumerable.Empty<string>();

        protected void LoadAndAnalyze(Stream code) {
            PythonAst ast;
            using (code) {
                var sink = new CollectingErrorSink();
                using (var sr = new StreamReader(code, Encoding.UTF8, true, 4096, true)) {
                    var parser = Parser.CreateParser(sr, Interpreter.LanguageVersion, new ParserOptions { ErrorSink = sink, StubFile = true });
                    ast = parser.ParseFile();
                }

                ParseErrors = sink.Errors.Select(e => "{0} ({1}): {2}".FormatUI(FilePath ?? "(builtins)", e.Span, e.Message)).ToArray();
                if (ParseErrors.Any()) {
                    Log?.Log(TraceEventType.Error, "Parse", FilePath ?? "(builtins)");
                    foreach (var e in ParseErrors) {
                        Log?.Log(TraceEventType.Error, "Parse", e);
                    }
                }
            }

            var walker = PrepareWalker(ast);
            ast.Walk(walker);

            Members = walker.GlobalScope.Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            PostWalk(walker);
        }

        protected virtual AstAnalysisWalker PrepareWalker(PythonAst ast) => new AstAnalysisWalker(this, ast, suppressBuiltinLookup: false);

        protected virtual void PostWalk(PythonWalker walker) => (walker as AstAnalysisWalker)?.Complete();
    }
}
