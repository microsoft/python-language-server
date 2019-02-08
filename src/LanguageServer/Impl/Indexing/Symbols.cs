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
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Indexing {

    internal class FunctionKind {
        public const string None = "";
        public const string Function = "function";
        public const string Property = "property";
        public const string StaticMethod = "staticmethod";
        public const string ClassMethod = "classmethod";
        public const string Class = "class";
    }

    // Analagous to LSP's DocumentSymbol.
    internal class HierarchicalSymbol {
        public string Name;
        public string Detail;
        public SymbolKind Kind;
        public bool? Deprecated;
        public SourceSpan Range;
        public SourceSpan SelectionRange;
        public IList<HierarchicalSymbol> Children;

        public string _functionKind;

        public HierarchicalSymbol(
            string name,
            SymbolKind kind,
            SourceSpan range,
            SourceSpan? selectionRange = null,
            IList<HierarchicalSymbol> children = null,
            string functionKind = FunctionKind.None
        ) {
            Name = name;
            Kind = kind;
            Range = range;
            SelectionRange = selectionRange ?? range;
            Children = children;
            _functionKind = functionKind;
        }
    }

    // Analagous to LSP's SymbolInformation.
    internal class FlatSymbol {
        public string Name;
        public SymbolKind Kind;
        public bool? Deprecated;
        public string DocumentPath;
        public SourceSpan Range;
        public string ContainerName;

        public FlatSymbol(
            string name,
            SymbolKind kind,
            string documentPath,
            SourceSpan range,
            string containerName = null
        ) {
            Name = name;
            Kind = kind;
            DocumentPath = documentPath;
            Range = range;
            ContainerName = containerName;
        }
    }
}
