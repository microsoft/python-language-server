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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public override async Task<CompletionList> Completion(CompletionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            TraceMessage($"Completions in {uri} at {@params.position}");

            var res = new CompletionList();
            //await InvokeExtensionsAsync((ext, token)
            //    => (ext as ICompletionExtension)?.HandleCompletionAsync(uri, analysis, tree, @params.position, res, cancellationToken), cancellationToken);
            return res;
        }

        //private SourceSpan? GetApplicableSpan(CompletionAnalysis ca, CompletionParams @params, PythonAst tree) {
        //    if (ca.ApplicableSpan.HasValue) {
        //        return ca.ApplicableSpan;
        //    }

        //    SourceLocation trigger = @params.position;
        //    if (ca.Node != null) {
        //        var span = ca.Node.GetSpan(tree);
        //        if (@params.context?.triggerKind == CompletionTriggerKind.TriggerCharacter) {
        //            if (span.End > trigger) {
        //                // Span start may be after the trigger if there is bunch of whitespace
        //                // between dot and next token such as in 'sys  .  version'
        //                span = new SourceSpan(new SourceLocation(span.Start.Line, Math.Min(span.Start.Column, trigger.Column)), span.End);
        //            }
        //        }
        //        if (span.End != span.Start) {
        //            return span;
        //        }
        //    }

        //    if (@params.context?.triggerKind == CompletionTriggerKind.TriggerCharacter) {
        //        var ch = @params.context?.triggerCharacter.FirstOrDefault() ?? '\0';
        //        return new SourceSpan(
        //            trigger.Line,
        //            Tokenizer.IsIdentifierStartChar(ch) ? Math.Max(1, trigger.Column - 1) : trigger.Column,
        //            trigger.Line,
        //            trigger.Column
        //        );
        //    }

        //    return null;
        //}

        public override Task<CompletionItem> CompletionItemResolve(CompletionItem item, CancellationToken token) {
            // TODO: Fill out missing values in item
            return Task.FromResult(item);
        }
    }
}
