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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        private readonly Stack<FunctionDefinition> _callEvalStack = new Stack<FunctionDefinition>();

        public IMember GetValueFromCallable(CallExpression expr) {
            if (expr?.Target == null) {
                return null;
            }

            var target = GetValueFromExpression(expr.Target);
            var result = GetValueFromGeneric(target, expr);
            if (result != null) {
                return result;
            }

            // Should only be two types of returns here. First, an bound type
            // so we can invoke Call over the instance. Second, an type info
            // so we can create an instance of the type (as in C() where C is class).
            IMember value = null;
            switch (target) {
                case IPythonBoundType bt: // Bound property, method or an iterator.
                    value = GetValueFromBound(bt, expr);
                    break;
                case IPythonInstance pi:
                    value = GetValueFromInstanceCall(pi, expr);
                    break;
                case IPythonFunctionType ft: // Standalone function or a class method call.
                    var instance = ft.DeclaringType != null ? new PythonInstance(ft.DeclaringType) : null;
                    value = GetValueFromFunctionType(ft, instance, expr);
                    break;
                case IPythonClassType cls:
                    value = GetValueFromClassCtor(cls, expr);
                    break;
                case IPythonType t:
                    // Target is type (info), the call creates instance.
                    // For example, 'x = C; y = x()' or 'x = C()' where C is class
                    value = new PythonInstance(t, GetLoc(expr));
                    break;
            }

            if (value == null) {
                Log?.Log(TraceEventType.Verbose, $"Unknown callable: {expr.Target.ToCodeString(Ast).Trim()}");
            }

            return value;
        }

        public IMember GetValueFromClassCtor(IPythonClassType cls, CallExpression expr) {
            SymbolTable.Evaluate(cls.ClassDefinition);
            // Determine argument types
            var args = ArgumentSet.Empty;
            var init = cls.GetMember<IPythonFunctionType>(@"__init__");
            if (init != null) {
                var a = new ArgumentSet(init, 0, new PythonInstance(cls), expr, Module, this);
                if (a.Errors.Count > 0) {
                    // AddDiagnostics(Module.Uri, a.Errors);
                }
                args = a.Evaluate();
            }
            return cls.CreateInstance(cls.Name, GetLoc(expr), args);
        }

        public IMember GetValueFromBound(IPythonBoundType t, CallExpression expr) {
            switch (t.Type) {
                case IPythonFunctionType fn:
                    return GetValueFromFunctionType(fn, t.Self, expr);
                case IPythonPropertyType p:
                    return GetValueFromProperty(p, t.Self);
                case IPythonIteratorType it when t.Self is IPythonCollection seq:
                    return seq.GetIterator();
            }
            return UnknownType;
        }

        public IMember GetValueFromInstanceCall(IPythonInstance pi, CallExpression expr) {
            // Call on an instance such as 'a = 1; a()'
            // If instance is a function (such as an unbound method), then invoke it.
            var type = pi.GetPythonType();
            if (type is IPythonFunctionType pft) {
                return GetValueFromFunctionType(pft, pi, expr);
            }

            // Try using __call__
            var call = type.GetMember("__call__").GetPythonType<IPythonFunctionType>();
            if (call != null) {
                return GetValueFromFunctionType(call, pi, expr);
            }

            // Optimistically return the instance itself. This happens when the call is
            // over 'function' that was actually replaced by a instance of a type.
            return pi;
        }

        public IMember GetValueFromFunctionType(IPythonFunctionType fn, IPythonInstance instance, CallExpression expr) {
            // If order to be able to find matching overload, we need to know
            // parameter types and count. This requires function to be analyzed.
            // Since we don't know which overload we will need, we have to 
            // process all known overloads for the function.
            foreach (var o in fn.Overloads) {
                SymbolTable.Evaluate(o.FunctionDefinition);
            }

            // Pick the best overload.
            FunctionDefinition fd;
            ArgumentSet args;
            if (fn.Overloads.Count == 1) {
                fd = fn.Overloads[0].FunctionDefinition;
                args = new ArgumentSet(fn, 0, instance, expr, this);
                args = args.Evaluate();
            } else {
                args = FindOverload(fn, instance, expr);
                if (args == null) {
                    return UnknownType;
                }
                fd = fn.Overloads.Count > 0 ? fn.Overloads[args.OverloadIndex].FunctionDefinition : null;
            }

            // Re-declare parameters in the function scope since originally
            // their types might not have been known and now argument set
            // may contain concrete values.
            if (fd != null) {
                using (OpenScope(fn.DeclaringModule, fn.FunctionDefinition, out _)) {
                    args.DeclareParametersInScope(this);
                }
            }

            // If instance is not the same as the declaring type, then call
            // most probably comes from the derived class which means that
            // the original 'self' and 'cls' variables are no longer valid
            // and function has to be re-evaluated with new arguments.
            // Note that there is nothing to re-evaluate in stubs.
            var instanceType = instance?.GetPythonType();
            if (instanceType == null || fn.DeclaringType == null || fn.IsSpecialized ||
                instanceType.IsSpecialized || fn.DeclaringType.IsSpecialized ||
                instanceType.Equals(fn.DeclaringType) ||
                fn.IsStub || !string.IsNullOrEmpty(fn.Overloads[args.OverloadIndex].GetReturnDocumentation(null))) {

                if (fn.IsSpecialized && fn is PythonFunctionType ft && ft.Dependencies.Count > 0) {
                    var dependencies = ImmutableArray<IPythonModule>.Empty;
                    foreach (var moduleName in ft.Dependencies) {
                        var dependency = Interpreter.ModuleResolution.GetOrLoadModule(moduleName);
                        if (dependency != null) {
                            dependencies = dependencies.Add(dependency);
                        }
                    }
                    Services.GetService<IPythonAnalyzer>().EnqueueDocumentForAnalysis(Module, dependencies);
                }

                var t = instance?.Call(fn.Name, args) ?? fn.Call(null, fn.Name, args);
                if (!t.IsUnknown()) {
                    return t;
                }
            }

            // Try and evaluate with specific arguments. Note that it does not
            // make sense to evaluate stubs since they already should be annotated.
            if (fn.DeclaringModule is IDocument doc && fd?.IsInAst(doc.GetAnyAst()) == true) {
                // Stubs are coming from another module.
                return TryEvaluateWithArguments(fn.DeclaringModule, fd, args);
            }
            return UnknownType;
        }

        public IMember GetValueFromProperty(IPythonPropertyType p, IPythonInstance instance) {
            // Function may not have been walked yet. Do it now.
            SymbolTable.Evaluate(p.FunctionDefinition);
            return instance.Call(p.Name, ArgumentSet.Empty);
        }

        private IMember TryEvaluateWithArguments(IPythonModule module, FunctionDefinition fd, IArgumentSet args) {
            // Attempt to evaluate with specific arguments but prevent recursion.
            IMember result = UnknownType;
            if (fd != null && !_callEvalStack.Contains(fd)) {
                using (OpenScope(module, fd.Parent, out _)) {
                    _callEvalStack.Push(fd);
                    try {
                        // TODO: cache results per function + argument set?
                        var eval = new FunctionCallEvaluator(module, fd, this);
                        result = eval.EvaluateCall(args);
                    } finally {
                        _callEvalStack.Pop();
                    }
                }
            }
            return result;
        }

        private ArgumentSet FindOverload(IPythonFunctionType fn, IPythonInstance instance, CallExpression expr) {
            if (fn.Overloads.Count == 1) {
                return null;
            }

            var sets = new List<ArgumentSet>();
            for (var i = 0; i < fn.Overloads.Count; i++) {
                var a = new ArgumentSet(fn, i, instance, expr, this);
                var args = a.Evaluate();
                sets.Add(args);
            }

            var orderedSets = sets.OrderBy(s => s.Errors.Count);
            var noErrorsMatches = sets.OrderBy(s => s.Errors.Count).TakeWhile(e => e.Errors.Count == 0).ToArray();
            var result = noErrorsMatches.Any()
                ? noErrorsMatches.FirstOrDefault(args => IsMatch(args, fn.Overloads[args.OverloadIndex].Parameters))
                : null;

            // Optimistically pick the best available.
            return result ?? orderedSets.FirstOrDefault();
        }

        private static bool IsMatch(IArgumentSet args, IReadOnlyList<IParameterInfo> parameters) {
            // Arguments passed to function are created off the function definition
            // and hence match by default. However, if multiple overloads are specified,
            // we need to figure out if annotated types match. 
            // https://docs.python.org/3/library/typing.html#typing.overload
            //
            //  @overload
            //  def process(response: None) -> None:
            //  @overload
            //  def process(response: int) -> Tuple[int, str]:
            //
            // Note that in overloads there are no * or ** parameters.
            // We match loosely by type.

            var d = parameters.ToDictionary(p => p.Name, p => p.Type);
            foreach (var a in args.Arguments<IMember>()) {
                if (!d.TryGetValue(a.Key, out var t)) {
                    return false;
                }

                var at = a.Value?.GetPythonType();
                if (t == null && at == null) {
                    continue;
                }

                if (t != null && at != null && !t.Equals(at)) {
                    return false;
                }
            }
            return true;
        }
    }
}
