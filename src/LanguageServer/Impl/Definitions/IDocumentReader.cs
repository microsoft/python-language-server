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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.Python.LanguageServer {
    public interface IDocumentReader {
        string ReadToEnd();
        string Read(int start, int count);
    }

    public static class DocumentReaderExtensions {
        public static string ReadLinearSpan(this IDocumentReader reader, LinearSpan span)
            => reader.Read(span.Start, span.Length);
        public static string ReadRange(this IDocumentReader reader, Range range, PythonAst ast)
            => reader.ReadLinearSpan(range.ToLinearSpan(ast));
        public static string ReadSourceSpan(this IDocumentReader reader, SourceSpan span, PythonAst ast)
            => reader.ReadLinearSpan(span.ToLinearSpan(ast));
    }
}
