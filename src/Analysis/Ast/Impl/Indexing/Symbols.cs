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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Indexing {
    // From LSP.
    internal enum SymbolKind {
        None = 0,
        File = 1,
        Module = 2,
        Namespace = 3,
        Package = 4,
        Class = 5,
        Method = 6,
        Property = 7,
        Field = 8,
        Constructor = 9,
        Enum = 10,
        Interface = 11,
        Function = 12,
        Variable = 13,
        Constant = 14,
        String = 15,
        Number = 16,
        Boolean = 17,
        Array = 18,
        Object = 19,
        Key = 20,
        Null = 21,
        EnumMember = 22,
        Struct = 23,
        Event = 24,
        Operator = 25,
        TypeParameter = 26
    }

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
