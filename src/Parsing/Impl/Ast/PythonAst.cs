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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    /// <summary>
    /// Top-level ast for all Python code. Holds onto the body and the line mapping information.
    /// </summary>
    public sealed class PythonAst : ScopeStatement, IBindableNode, ILocationConverter {
        private readonly object _lock = new object();
        private readonly Statement _body;
        private readonly Dictionary<Node, Dictionary<object, object>> _attributes = new Dictionary<Node, Dictionary<object, object>>();

        public PythonAst(Uri module, Statement body, NewLineLocation[] lineLocations, PythonLanguageVersion langVersion, SourceLocation[] commentLocations) {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            Module = module;
            LanguageVersion = langVersion;
            NewLineLocations = lineLocations;
            CommentLocations = commentLocations;
            ScopeInfo = new AstScopeInfo(this);
        }

        public PythonAst(IEnumerable<PythonAst> existingAst) {
            var asts = existingAst.ToArray();
            _body = new SuiteStatement(asts.Select(a => a.Body).ToArray());
            LanguageVersion = asts.Select(a => a.LanguageVersion).Distinct().Single();
            var locs = new List<NewLineLocation>();
            var comments = new List<SourceLocation>();
            var offset = 0;
            foreach (var a in asts) {
                locs.AddRange(a.NewLineLocations.Select(ll => new NewLineLocation(ll.EndIndex + offset, ll.Kind)));
                offset = locs.LastOrDefault().EndIndex;
            }
            NewLineLocations = locs.ToArray();
            offset = 0;
            foreach (var a in asts) {
                comments.AddRange(a.CommentLocations.Select(cl => new SourceLocation(cl.Line + offset, cl.Column)));
                offset += a.NewLineLocations.Length + 1;
            }
            CommentLocations = comments.ToArray();
            ScopeInfo = new AstScopeInfo(this);
        }

        public Uri Module { get; }
        public NewLineLocation[] NewLineLocations { get; private set; }
        public SourceLocation[] CommentLocations { get; private set; }

        /// <summary>
        /// Gets the class name which this AST was parsed under.  The class name is appended to any member
        /// accesses that occur.
        /// </summary>
        public string PrivatePrefix { get; internal set; }

        /// <summary>
        /// True if the AST was created with verbatim strings.
        /// </summary>
        public bool HasVerbatim { get; internal set; }

        public override IEnumerable<Node> GetChildNodes() => new[] { _body };

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _body.Walk(walker);
            }

            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                await _body.WalkAsync(walker, cancellationToken);
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        #region IScopeStatement
        public override Statement Body => _body;
        #endregion

        #region ScopeStatement
        public override string Name => "<module>";
        internal override ScopeInfo ScopeInfo { get; }
        #endregion

        public PythonLanguageVersion LanguageVersion { get; }

        public void Reduce(Func<Statement, bool> filter) {
            lock (_lock) {
                (Body as SuiteStatement)?.FilterStatements(filter);
                _attributes?.Clear();
                ScopeInfo.Variables?.Clear();
                CommentLocations = Array.Empty<SourceLocation>();
                // DO keep NewLineLocations as they are required
                // to calculate node positions for navigation;
                ScopeInfo.Clear();
            }
        }

        public bool TryGetAttribute(Node node, object key, out object value) {
            lock (_lock) {
                if (_attributes.TryGetValue(node, out var nodeAttrs)) {
                    return nodeAttrs.TryGetValue(key, out value);
                }

                value = null;
                return false;
            }
        }

        public void SetAttribute(Node node, object key, object value) {
            lock (_lock) {
                if (!_attributes.TryGetValue(node, out var nodeAttrs)) {
                    nodeAttrs = _attributes[node] = new Dictionary<object, object>();
                }

                nodeAttrs[key] = value;
            }
        }

        internal void SetAttributes(Dictionary<Node, Dictionary<object, object>> attributes) {
            lock (_lock) {
                foreach (var nodeAttributes in attributes) {
                    var node = nodeAttributes.Key;
                    if (!_attributes.TryGetValue(node, out var existingNodeAttributes)) {
                        existingNodeAttributes = _attributes[node] = new Dictionary<object, object>(nodeAttributes.Value.Count);
                    }

                    foreach (var nodeAttr in nodeAttributes.Value) {
                        existingNodeAttributes[nodeAttr.Key] = nodeAttr.Value;
                    }
                }
            }
        }

        #region ILocationConverter
        public SourceLocation IndexToLocation(int index) => NewLineLocation.IndexToLocation(NewLineLocations, index);
        public int LocationToIndex(SourceLocation location) => NewLineLocation.LocationToIndex(NewLineLocations, location, EndIndex);
        #endregion

        internal int GetLineEndFromPosition(int index) {
            var loc = IndexToLocation(index);
            if (loc.Line >= NewLineLocations.Length) {
                return index;
            }

            var res = NewLineLocations[loc.Line - 1];
            switch (res.Kind) {
                case NewLineKind.LineFeed:
                case NewLineKind.CarriageReturn: return res.EndIndex - 1;
                case NewLineKind.CarriageReturnLineFeed: return res.EndIndex - 2;
                default:
                    throw new InvalidOperationException("Bad line ending info");
            }
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            _body.AppendCodeString(res, ast, format);
            res.Append(this.GetExtraVerbatimText(ast));
        }
    }
}
