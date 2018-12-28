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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal partial class AnalysisWalker {
        public override async Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) {
            if (node.IsLambda || _replacedByStubs.Contains(node)) {
                return false;
            }

            var cls = Lookup.GetInScope("__class__").GetPythonType();
            var loc = GetLoc(node);
            if (ProcessFunctionDecorators(node, cls, loc)) {
                ProcessFunctionDefinition(node, cls, loc);
            }
            // Do not recurse into functions
            return false;
        }

        public void ProcessFunctionDefinition(FunctionDefinition node, IPythonType declaringType, LocationInfo loc) {
            if (!(Lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is PythonFunctionType existing)) {
                existing = new PythonFunctionType(node, Module, declaringType, loc);
                Lookup.DeclareVariable(node.Name, existing, loc);
            }
            AddOverload(node, existing, o => existing.AddOverload(o));
        }


        private void AddProperty(FunctionDefinition node, IPythonModuleType declaringModule, IPythonType declaringType, bool isAbstract, LocationInfo loc) {
            if (!(Lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is PythonPropertyType existing)) {
                existing = new PythonPropertyType(node, declaringModule, declaringType, isAbstract, loc);
                Lookup.DeclareVariable(node.Name, existing, loc);
            }
            AddOverload(node, existing, o => existing.AddOverload(o));
        }

        private PythonFunctionOverload CreateFunctionOverload(ExpressionLookup lookup, FunctionDefinition node, IPythonClassMember function) {
            var parameters = node.Parameters
                .Select(p => new ParameterInfo(Ast, p, Lookup.GetTypeFromAnnotation(p.Annotation)))
                .ToArray();

            var overload = new PythonFunctionOverload(
                node.Name,
                parameters,
                lookup.GetLocOfName(node, node.NameExpression),
                node.ReturnAnnotation?.ToCodeString(Ast));

            FunctionWalkers.Add(new AnalysisFunctionWalker(lookup, node, overload, function));
            return overload;
        }

        private void AddOverload(FunctionDefinition node, IPythonClassMember function, Action<IPythonFunctionOverload> addOverload) {
            // Check if function exists in stubs. If so, take overload from stub
            // and the documentation from this actual module.
            var stubOverload = GetOverloadFromStub(node);
            if (stubOverload != null) {
                if (!string.IsNullOrEmpty(node.Documentation)) {
                    stubOverload.SetDocumentation(node.Documentation);
                }
                addOverload(stubOverload);
                _replacedByStubs.Add(node);
                return;
            }

            if (!FunctionWalkers.Contains(node)) {
                var overload = CreateFunctionOverload(Lookup, node, function);
                if (overload != null) {
                    addOverload(overload);
                }
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

        private bool ProcessFunctionDecorators(FunctionDefinition node, IPythonType declaringType, LocationInfo location) {
            var dec = node.Decorators?.Decorators;
            var decorators = dec != null ? dec.ExcludeDefault().ToArray() : Array.Empty<Expression>();

            foreach (var d in decorators.OfType<NameExpression>()) {
                switch (d.Name) {
                    case "property":
                        AddProperty(node, Module, declaringType, false, location);
                        return false;
                    case "abstractproperty":
                        AddProperty(node, Module, declaringType, true, location);
                        return false;
                }
            }
            return true;
        }
    }
}
