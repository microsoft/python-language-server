using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Analysis.Indexing {
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

    // Analagous to LSP's DocumentSymbol.
    internal class HierarchicalSymbol {
        public string Name;
        public string Detail;
        public SymbolKind Kind;
        public bool? Deprecated;
        public SourceSpan Range;
        public SourceSpan SelectionRange;
        public List<HierarchicalSymbol> Children;
    }

    // Analagous to LSP's SymbolInformation.
    internal class FlatSymbol {
        public string Name;
        public SymbolKind Kind;
        public bool? Deprecated;
        public Uri DocumentUri;
        public SourceSpan Range;
        public string ContainerName;
    }
}
