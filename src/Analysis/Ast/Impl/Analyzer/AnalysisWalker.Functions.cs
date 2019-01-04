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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal partial class AnalysisWalker {
        public override Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) {
            if (node.IsLambda || ReplacedByStubs.Contains(node)) {
                return Task.FromResult(false);
            }

            // This part only adds definition for the function and its overloads
            // to the walker list. It does NOT resolve return types or parameters.
            // Function body is not walked. For the actual function code walk
            // and the type resolution see FunctionWalker class.
            AddFunctionOrProperty(node);
            // Do not recurse into functions
            return Task.FromResult(false);
        }

        protected void AddFunction(FunctionDefinition node, IPythonType declaringType, LocationInfo loc) {
            if (!(Eval.LookupNameInScopes(node.Name, LookupOptions.Local) is PythonFunctionType existing)) {
                existing = new PythonFunctionType(node, Module, declaringType, loc);
                Eval.DeclareVariable(node.Name, existing, loc);
            }
            AddOverload(node, existing, o => existing.AddOverload(o));
        }

        protected virtual IPythonClassType GetSelf() => Eval.GetInScope("__class__")?.GetPythonType<IPythonClassType>();

        protected void AddFunctionOrProperty(FunctionDefinition fd) {
            var cls = GetSelf();
            var loc = GetLoc(fd);
            if (!TryAddProperty(fd, cls, loc)) {
                AddFunction(fd, cls, loc);
            }
        }

        private void AddOverload(FunctionDefinition node, IPythonClassMember function, Action<IPythonFunctionOverload> addOverload) {
            // Check if function exists in stubs. If so, take overload from stub
            // and the documentation from this actual module.
            if (!ReplacedByStubs.Contains(node)) {
                var stubOverload = GetOverloadFromStub(node);
                if (stubOverload != null) {
                    if (!string.IsNullOrEmpty(node.Documentation)) {
                        stubOverload.SetDocumentationProvider(_ => node.Documentation);
                    }

                    addOverload(stubOverload);
                    ReplacedByStubs.Add(node);
                    return;
                }
            }

            if (!MemberWalkers.Contains(node)) {
                // Do not evaluate parameter types just yet. During light-weight top-level information
                // collection types cannot be determined as imports haven't been processed.
                var location = Eval.GetLocOfName(node, node.NameExpression);
                var returnDoc = node.ReturnAnnotation?.ToCodeString(Ast);
                var overload = new PythonFunctionOverload(node, Module, location, returnDoc);
                addOverload(overload);
                MemberWalkers.Add(new FunctionWalker(Eval, node, overload, function, GetSelf()));
            }
        }

        private PythonFunctionOverload GetOverloadFromStub(FunctionDefinition node) {
            var t = GetMemberFromStub(node.Name).GetPythonType();
            if (t is IPythonFunctionType f) {
                return f.Overloads
                    .OfType<PythonFunctionOverload>()
                    .FirstOrDefault(o => o.Parameters.Count == node.Parameters.Length);
            }
            return null;
        }

        private bool TryAddProperty(FunctionDefinition node, IPythonType declaringType, LocationInfo location) {
            var dec = node.Decorators?.Decorators;
            var decorators = dec != null ? dec.ExcludeDefault().ToArray() : Array.Empty<Expression>();

            foreach (var d in decorators.OfType<NameExpression>()) {
                switch (d.Name) {
                    case @"property":
                        AddProperty(node, Module, declaringType, false, location);
                        return true;
                    case @"abstractproperty":
                        AddProperty(node, Module, declaringType, true, location);
                        return true;
                }
            }
            return false;
        }

        private void AddProperty(FunctionDefinition node, IPythonModule declaringModule, IPythonType declaringType, bool isAbstract, LocationInfo loc) {
            if (!(Eval.LookupNameInScopes(node.Name, LookupOptions.Local) is PythonPropertyType existing)) {
                existing = new PythonPropertyType(node, declaringModule, declaringType, isAbstract, loc);
                Eval.DeclareVariable(node.Name, existing, loc);
            }
            AddOverload(node, existing, o => existing.AddOverload(o));
        }
    }
}
