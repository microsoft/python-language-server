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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

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
            var ctxt = new CompletionAnalysis(analysis, tree, @params.position, opts, _displayTextBuilder, this,
                () => entry.ReadDocument(ProjectFiles.GetPart(uri), out _));

            var members = ctxt.GetCompletionsFromString(@params._expr) ?? ctxt.GetCompletions();
            if (members == null) {
                TraceMessage($"Do not trigger at {@params.position} in {uri}");
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

            SourceLocation trigger = @params.position;
            if (ctxt.ApplicableSpan.HasValue) {
                res._applicableSpan = ctxt.ApplicableSpan;
            } else if (ctxt.Node != null) {
                var span = ctxt.Node.GetSpan(tree);
                if (@params.context?.triggerKind == CompletionTriggerKind.TriggerCharacter) {
                    if (span.End > trigger) {
                        span = new SourceSpan(span.Start, trigger);
                    }
                }
                if (span.End != span.Start) {
                    res._applicableSpan = span;
                }
            } else if (@params.context?.triggerKind == CompletionTriggerKind.TriggerCharacter) {
                var ch = @params.context?.triggerCharacter.FirstOrDefault() ?? '\0';
                res._applicableSpan = new SourceSpan(
                    trigger.Line,
                    Tokenizer.IsIdentifierStartChar(ch) ? Math.Max(1, trigger.Column - 1) : trigger.Column,
                    trigger.Line,
                    trigger.Column
                );
            }

            LogMessage(MessageType.Info, $"Found {res.items.Length} completions for {uri} at {@params.position} after filtering");

            if (HandleOldStyleCompletionExtension(analysis as ModuleAnalysis, tree, @params.position, res)) {
                return res;
            }

            await InvokeExtensionsAsync((ext, token)
                => (ext as ICompletionExtension)?.HandleCompletionAsync(uri, analysis, tree, @params.position, res, cancellationToken), cancellationToken);

            return res;
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

        private bool HandleOldStyleCompletionExtension(ModuleAnalysis analysis, PythonAst tree, SourceLocation location, CompletionList completions) {
            if (_oldServer == null) {
                return false;
            }
            // Backward compatibility case
            var cl = new PythonTools.Analysis.LanguageServer.CompletionList {
                items = completions.items.Select(x => new PythonTools.Analysis.LanguageServer.CompletionItem {
                    // Partial copy
                    label = x.label,
                    kind = (PythonTools.Analysis.LanguageServer.CompletionItemKind)x.kind,
                    detail = x.detail,
                    sortText = x.sortText,
                    filterText = x.filterText,
                    preselect = x.preselect,
                    insertText = x.insertText,
                }).ToArray()
            };

            var oldItems = new HashSet<string>();
            foreach (var x in completions.items) {
                oldItems.Add(x.label);
            }

            _oldServer.ProcessCompletionList(analysis as ModuleAnalysis, tree, location, cl);

            var newItems = cl.items.Where(x => !oldItems.Contains(x.label)).ToArray();
            if(newItems.Length == 0) {
                return false;
            }

            var converted = newItems.Select(x => new CompletionItem {
                label = x.label,
                kind = (CompletionItemKind)x.kind,
                detail = x.detail,
                sortText = x.sortText,
                filterText = x.filterText,
                preselect = x.preselect,
                insertText = x.insertText,
                textEdit = x.textEdit.HasValue
                    ? new TextEdit {
                        range = new Range {
                            start = new Position {
                                line = x.textEdit.Value.range.start.line,
                                character = x.textEdit.Value.range.start.character,
                            },
                            end = new Position {
                                line = x.textEdit.Value.range.end.line,
                                character = x.textEdit.Value.range.end.character,
                            }
                        },
                        newText = x.textEdit.Value.newText
                    } : (TextEdit?)null,
                command = x.command.HasValue
                    ? new Command {
                        title = x.command.Value.title,
                        command = x.command.Value.command,
                        arguments = x.command.Value.arguments
                    } : (Command?)null,
                data = x.data
            });

            completions.items = completions.items.Concat(converted).ToArray();
            return true;
        }
    }
}
