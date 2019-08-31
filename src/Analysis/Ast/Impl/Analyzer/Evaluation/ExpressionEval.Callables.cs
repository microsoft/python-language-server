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
using Microsoft.Python.Analysis.Extensions;
using Microsoft.Python.Analysis.Modules;
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
            target?.AddReference(GetLocationOfName(expr.Target));

            var result = GetValueFromGeneric(target, expr);
            if (result != null) {
                return result;
            }

            // Should only be two types of returns here. First, an bound type
            // so we can invoke Call over the instance. Second, an type info
            // so we can create an instance of the type (as in C() where C is class).
            IMember value = null;
            var args = ArgumentSet.Empty(expr, this);
            switch (target) {
                case IPythonBoundType bt: // Bound property, method or an iterator.
                    value = GetValueFromBound(bt, expr);
                    break;
                case IPythonInstance pi:
                    value = GetValueFromInstanceCall(pi, expr);
                    break;
                case IPythonFunctionType ft: // Standalone function or a class method call.
                    var instance = ft.DeclaringType != null ? ft.DeclaringType.CreateInstance(args) : null;
                    value = GetValueFromFunctionType(ft, instance, expr);
                    break;
                case IPythonClassType cls:
                    value = GetValueFromClassCtor(cls, expr);
                    break;
                case IPythonType t:
                    // Target is type (info), the call creates instance.
                    // For example, 'x = C; y = x()' or 'x = C()' where C is class
                    value = t.CreateInstance(args);
                    break;
            }

            if (value == null) {
                Log?.Log(TraceEventType.Verbose, $"Unknown callable: {expr.Target.ToCodeString(Ast).Trim()}");
            }

            return value;
        }

        public IMember GetValueFromLambda(LambdaExpression expr) {
            if (expr == null) {
                return null;
            }

            var fd = expr.Function;
            var location = GetLocationOfName(fd);
            var ft = new PythonFunctionType(fd, null, location);
            var overload = new PythonFunctionOverload(ft, fd, location, expr.Function.ReturnAnnotation?.ToCodeString(Ast));
            overload.SetParameters(CreateFunctionParameters(null, ft, fd, false));
            ft.AddOverload(overload);
            return ft;
        }

        public IMember GetValueFromClassCtor(IPythonClassType cls, CallExpression expr) {
            SymbolTable.Evaluate(cls.ClassDefinition);
            // Determine argument types
            var args = ArgumentSet.Empty(expr, this);
            var init = cls.GetMember<IPythonFunctionType>(@"__init__");
            if (init != null) {
                using (OpenScope(cls.DeclaringModule, cls.ClassDefinition, out _)) {
                    var a = new ArgumentSet(init, 0, cls, expr, this);
                    if (a.Errors.Count > 0) {
                        // AddDiagnostics(Module.Uri, a.Errors);
                    }
                    args = a.Evaluate();
                }
            }
            return cls.CreateInstance(args);
        }

        private IMember GetValueFromBound(IPythonBoundType t, CallExpression expr) {
            switch (t.Type) {
                case IPythonFunctionType fn:
                    return GetValueFromFunctionType(fn, t.Self, expr);
                case IPythonPropertyType p:
                    return GetValueFromProperty(p, t.Self, expr);
                case IPythonIteratorType _ when t.Self is IPythonCollection seq:
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
            var instanceType = instance?.GetPythonType();

            if (fn.Overloads.Count == 1) {
                fd = fn.Overloads[0].FunctionDefinition;
                args = new ArgumentSet(fn, 0, instanceType, expr, this);
                args = args.Evaluate();
            } else {
                args = FindOverload(fn, instanceType, expr);
                if (args == null) {
                    return UnknownType;
                }
                fd = fn.Overloads.Count > 0 ? fn.Overloads[args.OverloadIndex].FunctionDefinition : null;
            }

            // Re-declare parameters in the function scope since originally
            // their types might not have been known and now argument set
            // may contain concrete values.
            if (fd != null && EvaluateFunctionBody(fn)) {
                using (OpenScope(fn.DeclaringModule, fn.FunctionDefinition, out _)) {
                    args.DeclareParametersInScope(this);
                }
            }

            // If instance type is not the same as the declaring type, then call most probably comes
            // from the derived class which means that the original 'self' and 'cls' variables
            // are no longer valid and function has to be re-evaluated with new arguments.
            // Note that there is nothing to re-evaluate in stubs.
            if (instanceType == null || fn.DeclaringType == null || fn.IsSpecialized ||
                instanceType.IsSpecialized || fn.DeclaringType.IsSpecialized ||
                instanceType.Equals(fn.DeclaringType) ||
                fn.IsStub || !string.IsNullOrEmpty(fn.Overloads[args.OverloadIndex].GetReturnDocumentation())) {

                LoadFunctionDependencyModules(fn);

                var m = instance?.Call(fn.Name, args) ?? fn.Call(null, fn.Name, args);
                if (!m.IsUnknown()) {
                    return m;
                }
            }

            // We could not tell the return type from the call. Here we try and evaluate with specific arguments.
            // Note that it does not make sense evaluating stubs or compiled/scraped modules since they
            // should be either annotated or static return type known from the analysis.
            //
            // Also, we do not evaluate library code with arguments for performance reasons.
            // This will prevent cases like
            //      def func(a, b): return a + b
            // from working in libraries, but this is small sacrifice for significant performance
            // increase in library analysis.
            if (fn.DeclaringModule is IDocument && EvaluateFunctionBody(fn)) {
                // Stubs are coming from another module.
                return TryEvaluateWithArguments(fn, args);
            }
            return UnknownType;
        }

        private IMember GetValueFromProperty(IPythonPropertyType p, IPythonInstance instance, CallExpression expr) {
            // Function may not have been walked yet. Do it now.
            SymbolTable.Evaluate(p.FunctionDefinition);
            return instance.Call(p.Name, ArgumentSet.Empty(expr, this));
        }


        private readonly Dictionary<int, IMember> _argEvalCache = new Dictionary<int, IMember>();

        private IMember TryEvaluateWithArguments(IPythonFunctionType fn, IArgumentSet args) {
            var name = fn.DeclaringType != null ? $"{fn.DeclaringModule.Name}.{fn.Name}" : fn.Name;
            var argHash = args
                .Arguments
                .Select(a => a.Name.GetHashCode() ^ 397 * (a.Value?.GetHashCode() ?? 0))
                .Aggregate(0, (current, d) => 31 * current ^ d);
            var key = fn.DeclaringModule.Name.GetHashCode() ^ name.GetHashCode() ^ (397 * argHash);

            if (_argEvalCache.TryGetValue(key, out var result)) {
                return result;
            }

            var fd = fn.FunctionDefinition;
            var module = fn.DeclaringModule;

            // Attempt to evaluate with specific arguments but prevent recursion.
            result = UnknownType;
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

            _argEvalCache[key] = result;
            return result;
        }

        private ArgumentSet FindOverload(IPythonFunctionType fn, IPythonType instanceType, CallExpression expr) {
            if (fn.Overloads.Count == 1) {
                return null;
            }

            var sets = new List<ArgumentSet>();
            for (var i = 0; i < fn.Overloads.Count; i++) {
                var a = new ArgumentSet(fn, i, instanceType, expr, this);
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

        private bool EvaluateFunctionBody(IPythonFunctionType fn)
            => fn.DeclaringModule.ModuleType != ModuleType.Library;

        private void LoadFunctionDependencyModules(IPythonFunctionType fn) {
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
        }

        public IReadOnlyList<IParameterInfo> CreateFunctionParameters(IPythonClassType self, IPythonClassMember function, FunctionDefinition fd, bool declareVariables) {
            // For class method no need to add extra parameters, but first parameter type should be the class.
            // For static and unbound methods do not add or set anything.
            // For regular bound methods add first parameter and set it to the class.

            var parameters = new List<ParameterInfo>();
            var skip = 0;
            if (self != null && function.HasClassFirstArgument()) {
                var p0 = fd.Parameters.FirstOrDefault();
                if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
                    var annType = GetTypeFromAnnotation(p0.Annotation, out var isGeneric);
                    // Actual parameter type will be determined when method is invoked.
                    // The reason is that if method might be called on a derived class.
                    // Declare self or cls in this scope.
                    if (declareVariables) {
                        DeclareVariable(p0.Name, self.CreateInstance(ArgumentSet.Empty(p0.NameExpression, this)),
                            VariableSource.Declaration, p0.NameExpression);
                    }
                    // Set parameter info, declare type as annotation type for generic self 
                    // e.g def test(self: T)
                    var pi = new ParameterInfo(Ast, p0, isGeneric ? annType : self, null, false);
                    parameters.Add(pi);
                    skip++;
                }
            }

            // Declare parameters in scope
            for (var i = skip; i < fd.Parameters.Length; i++) {
                var p = fd.Parameters[i];
                if (!string.IsNullOrEmpty(p.Name)) {
                    var defaultValue = GetValueFromExpression(p.DefaultValue);
                    var paramType = GetTypeFromAnnotation(p.Annotation, out var isGeneric) ?? UnknownType;
                    if (paramType.IsUnknown()) {
                        // If parameter has default value, look for the annotation locally first
                        // since outer type may be getting redefined. Consider 's = None; def f(s: s = 123): ...
                        paramType = GetTypeFromAnnotation(p.Annotation, out isGeneric, LookupOptions.Local | LookupOptions.Builtins);
                        // Default value of None does not mean the parameter is None, just says it can be missing.
                        defaultValue = defaultValue.IsUnknown() || defaultValue.IsOfType(BuiltinTypeId.NoneType) ? null : defaultValue;
                        if (paramType == null && defaultValue != null) {
                            paramType = defaultValue.GetPythonType();
                        }
                    }
                    // If all else fails, look up globally.
                    var pi = new ParameterInfo(Ast, p, paramType, defaultValue, isGeneric | paramType.IsGeneric());
                    if (declareVariables) {
                        DeclareParameter(p, pi);
                    }
                    parameters.Add(pi);
                } else if (p.IsList || p.IsDictionary) {
                    parameters.Add(new ParameterInfo(Ast, p, null, null, false));
                }
            }
            return parameters;
        }

        private void DeclareParameter(Parameter p, ParameterInfo pi) {
            IPythonType paramType;
            // If type is known from annotation, use it.
            if (pi != null && !pi.Type.IsUnknown() && !pi.Type.IsGenericParameter()) {
                // TODO: technically generics may have constraints. Should we consider them?
                paramType = pi.Type;
            } else {
                paramType = pi?.DefaultValue?.GetPythonType() ?? UnknownType;
            }
            DeclareVariable(p.Name, paramType.CreateInstance(ArgumentSet.Empty(p.NameExpression, this)),
                VariableSource.Declaration, p.NameExpression);
        }
    }
}
