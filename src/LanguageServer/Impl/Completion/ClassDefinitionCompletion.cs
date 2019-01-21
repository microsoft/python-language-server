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

using System.Linq;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class ClassDefinitionCompletion {
        public static bool NoCompletions(ClassDefinition cd, CompletionContext context, out bool addMetadataArg) {
            addMetadataArg = false;

            if (cd.HeaderIndex > cd.StartIndex && context.Position > cd.HeaderIndex) {
                return false;
            }

            if (context.Position == cd.HeaderIndex) {
                return true;
            }

            if (cd.Bases.Length > 0 && context.Position >= cd.Bases[0].StartIndex) {
                foreach (var p in cd.Bases.Reverse()) {
                    if (context.Position >= p.StartIndex) {
                        if (p.Name == null && context.Ast.LanguageVersion.Is3x() && cd.Bases.All(b => b.Name != @"metaclass")) {
                            addMetadataArg = true;
                        }

                        return false;
                    }
                }
            }

            if (cd.NameExpression != null && cd.NameExpression.StartIndex > cd.KeywordEndIndex && context.Position >= cd.NameExpression.StartIndex) {
                return true;
            }

            return context.Position > cd.KeywordEndIndex;
        }
    }
}
