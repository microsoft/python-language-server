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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class DocumentAnalysis : IDocumentAnalysis {
        public static readonly IDocumentAnalysis Empty = new EmptyAnalysis();

        public DocumentAnalysis(IDocument document, int version, IGlobalScope globalScope, PythonAst ast) {
            Check.ArgumentNotNull(nameof(document), document);
            Check.ArgumentNotNull(nameof(globalScope), globalScope);
            Document = document;
            Version = version;
            GlobalScope = globalScope;
            Ast = ast;
        }

        #region IDocumentAnalysis
        /// <summary>
        /// Analyzed document.
        /// </summary>
        public IDocument Document { get; }

        /// <summary>
        /// Version of the analysis. Usually matches document version,
        /// but can be lower when document or its dependencies were
        /// updated since.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// AST that was used in the analysis.
        /// </summary>
        public PythonAst Ast { get; }

        /// <summary>
        /// Document/module global scope.
        /// </summary>
        public IGlobalScope GlobalScope { get; }

        /// <summary>
        /// Module top-level members
        /// </summary>
        public IVariableCollection TopLevelVariables => GlobalScope.Variables;

        /// <summary>
        /// All module members from all scopes.
        /// </summary>
        public IEnumerable<IVariable> AllVariables
            => (GlobalScope as IScope).TraverseBreadthFirst(s => s.Children).SelectMany(s => s.Variables);

        public IEnumerable<IPythonType> GetAllAvailableItems(SourceLocation location) => Enumerable.Empty<IPythonType>();
        public IEnumerable<IPythonType> GetMembers(SourceLocation location) => Enumerable.Empty<IPythonType>();
        public IEnumerable<IPythonFunctionOverload> GetSignatures(SourceLocation location) => Enumerable.Empty<IPythonFunctionOverload>();
        public IEnumerable<IPythonType> GetValues(SourceLocation location) => Enumerable.Empty<IPythonType>();
        #endregion

        private sealed class EmptyAnalysis : IDocumentAnalysis {
            public EmptyAnalysis(IDocument document = null) {
                Document = document;
                GlobalScope = new EmptyGlobalScope(document);
            }

            public IDocument Document { get; }
            public int Version { get; } = -1;
            public IGlobalScope GlobalScope { get; }
            public PythonAst Ast => null;
            public IEnumerable<IPythonType> GetAllAvailableItems(SourceLocation location) => Enumerable.Empty<IPythonType>();
            public IEnumerable<DiagnosticsEntry> Diagnostics => Enumerable.Empty<DiagnosticsEntry>();
            public IVariableCollection TopLevelVariables => VariableCollection.Empty;
            public IEnumerable<IVariable> AllVariables => Enumerable.Empty<IVariable>();
            public IEnumerable<IPythonType> GetMembers(SourceLocation location) => Enumerable.Empty<IPythonType>();
            public IEnumerable<IPythonFunctionOverload> GetSignatures(SourceLocation location) => Enumerable.Empty<IPythonFunctionOverload>();
            public IEnumerable<IPythonType> GetValues(SourceLocation location) => Enumerable.Empty<IPythonType>();
        }
    }
}
