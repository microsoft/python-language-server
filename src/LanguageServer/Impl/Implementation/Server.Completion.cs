﻿// Python Tools for Visual Studio
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
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public override async Task<CompletionList> Completion(CompletionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;

            ProjectFiles.GetEntry(@params.textDocument, @params._version, out var entry, out var tree);
            TraceMessage($"Completions in {uri} at {@params.position}");

            tree = GetParseTree(entry, uri, cancellationToken, out var version) ?? tree;
            var analysis = entry != null ? await entry.GetAnalysisAsync(50, cancellationToken) : null;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return new CompletionList();
            }

            var opts = GetOptions(@params.context);
            var ctxt = new CompletionAnalysis(analysis, tree, @params.position, opts, Settings.completion, _displayTextBuilder, Logger, () => entry.ReadDocument(ProjectFiles.GetPart(uri), out _));

            var members = string.IsNullOrEmpty(@params._expr)
                ? ctxt.GetCompletions()
                : ctxt.GetCompletionsFromString(@params._expr);

            if (members == null) {
                TraceMessage($"No completions at {@params.position} in {uri}");
                return new CompletionList();
            }

            if (!Settings.completion.showAdvancedMembers) {
                members = members.Where(m => !m.label.StartsWith("__"));
            }

            var filterKind = @params.context?._filterKind;
            if (filterKind.HasValue && filterKind != CompletionItemKind.None) {
                TraceMessage($"Only returning {filterKind.Value} items");
                members = members.Where(m => m.kind == filterKind.Value);
            }

            var res = new CompletionList {
                items = members.ToArray(),
                _expr = ctxt.ParentExpression?.ToCodeString(tree, CodeFormattingOptions.Traditional),
                _commitByDefault = ctxt.ShouldCommitByDefault,
                _allowSnippet = ctxt.ShouldAllowSnippets
            };

            res._applicableSpan = GetApplicableSpan(ctxt, @params, tree);
            LogMessage(MessageType.Info, $"Found {res.items.Length} completions for {uri} at {@params.position} after filtering");

            await InvokeExtensionsAsync((ext, token)
                => (ext as ICompletionExtension)?.HandleCompletionAsync(uri, analysis, tree, @params.position, res, cancellationToken), cancellationToken);

            return res;
        }

        private SourceSpan? GetApplicableSpan(CompletionAnalysis ca, CompletionParams @params, PythonAst tree) {
            if (ca.ApplicableSpan.HasValue) {
                return ca.ApplicableSpan;
            }

            SourceLocation trigger = @params.position;
            if (ca.Node != null) {
                var span = ca.Node.GetSpan(tree);
                if (@params.context?.triggerKind == CompletionTriggerKind.TriggerCharacter) {
                    if (span.End > trigger) {
                        // Span start may be after the trigger if there is bunch of whitespace
                        // between dot and next token such as in 'sys  .  version'
                        span = new SourceSpan(new SourceLocation(span.Start.Line, Math.Min(span.Start.Column, trigger.Column)), span.End);
                    }
                }
                if (span.End != span.Start) {
                    return span;
                }
            }

            if (@params.context?.triggerKind == CompletionTriggerKind.TriggerCharacter) {
                var ch = @params.context?.triggerCharacter.FirstOrDefault() ?? '\0';
                return new SourceSpan(
                    trigger.Line,
                    Tokenizer.IsIdentifierStartChar(ch) ? Math.Max(1, trigger.Column - 1) : trigger.Column,
                    trigger.Line,
                    trigger.Column
                );
            }

            return null;
        }

        public override Task<CompletionItem> CompletionItemResolve(CompletionItem item, CancellationToken token) {
            // TODO: Fill out missing values in item
            return Task.FromResult(item);
        }

        private GetMemberOptions GetOptions(CompletionContext? context) {
            var opts = GetMemberOptions.None;
            if (context.HasValue) {
                var c = context.Value;
                if (c._intersection) {
                    opts |= GetMemberOptions.IntersectMultipleResults;
                }
            }
            return opts;
        }
    }
}
