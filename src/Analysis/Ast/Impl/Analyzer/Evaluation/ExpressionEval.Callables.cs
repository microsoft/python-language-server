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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Extensions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        public async Task<IMember> GetValueFromCallableAsync(CallExpression expr, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (expr?.Target == null) {
                return null;
            }

            var target = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            // Should only be two types of returns here. First, an bound type
            // so we can invoke Call over the instance. Second, an type info
            // so we can create an instance of the type (as in C() where C is class).
            IMember value = null;
            switch (target) {
                case IPythonBoundType bt: // Bound property, method or an iterator.
                    value = await GetValueFromBoundAsync(bt, expr, cancellationToken);
                    break;
                case IPythonInstance pi:
                    value = await GetValueFromInstanceCall(pi, expr, cancellationToken);
                    break;
                case IPythonFunctionType ft: // Standalone function or a class method call.
                    var instance = ft.DeclaringType != null ? new PythonInstance(ft.DeclaringType) : null;
                    value = await GetValueFromFunctionTypeAsync(ft, instance, expr, cancellationToken);
                    break;
                case IPythonClassType cls:
                    value = await GetValueFromClassCtorAsync(cls, expr, cancellationToken);
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

        public async Task<IMember> GetValueFromClassCtorAsync(IPythonClassType cls, CallExpression expr, CancellationToken cancellationToken = default) {
            await SymbolTable.EvaluateAsync(cls.ClassDefinition, cancellationToken);
            // Determine argument types
            var args = new List<IMember>();
            foreach (var a in expr.Args.MaybeEnumerate()) {
                var type = await GetValueFromExpressionAsync(a.Expression, cancellationToken);
                args.Add(type ?? UnknownType);
            }
            return cls.CreateInstance(cls.Name, GetLoc(expr), args);
        }

        public async Task<IMember> GetValueFromBoundAsync(IPythonBoundType t, CallExpression expr, CancellationToken cancellationToken = default) {
            switch(t.Type) {
                case IPythonFunctionType fn:
                    return await GetValueFromFunctionTypeAsync(fn, t.Self, expr, cancellationToken);
                case IPythonPropertyType p:
                    return await GetValueFromPropertyAsync(p, t.Self, cancellationToken);
                case IPythonIteratorType it when t.Self is IPythonCollection seq:
                    return seq.GetIterator();
            }
            return UnknownType;
        }

        public async Task<IMember> GetValueFromInstanceCall(IPythonInstance pi, CallExpression expr, CancellationToken cancellationToken = default) {
            // Call on an instance such as 'a = 1; a()'
            // If instance is a function (such as an unbound method), then invoke it.
            var type = pi.GetPythonType();
            if (type is IPythonFunctionType pft) {
                return await GetValueFromFunctionTypeAsync(pft, pi, expr, cancellationToken);
            }

            // Try using __call__
            var call = type.GetMember("__call__").GetPythonType<IPythonFunctionType>();
            if (call != null) {
                return await GetValueFromFunctionTypeAsync(call, pi, expr, cancellationToken);
            }

            return null;
        }

        public async Task<IMember> GetValueFromFunctionTypeAsync(IPythonFunctionType fn, IPythonInstance instance, CallExpression expr, CancellationToken cancellationToken = default) {
            // Determine argument types
            var args = new List<IMember>();
            // For static and regular methods add 'self' or 'cls'
            if (fn.HasClassFirstArgument()) {
                args.Add((IMember)instance ?? fn.DeclaringType ?? Interpreter.UnknownType);
            }

            if (expr != null) {
                foreach (var a in expr.Args.MaybeEnumerate()) {
                    var type = await GetValueFromExpressionAsync(a.Expression, cancellationToken);
                    args.Add(type ?? UnknownType);
                }
            }

            // If order to be able to find matching overload, we need to know
            // parameter types and count. This requires function to be analyzed.
            // Since we don't know which overload we will need, we have to 
            // process all known overloads for the function.
            foreach (var o in fn.Overloads) {
                await SymbolTable.EvaluateAsync(o.FunctionDefinition, cancellationToken);
            }
            return instance?.Call(fn.Name, args) ?? fn.Call(null, fn.Name, args);
        }

        public async Task<IMember> GetValueFromPropertyAsync(IPythonPropertyType p, IPythonInstance instance, CancellationToken cancellationToken = default) {
            // Function may not have been walked yet. Do it now.
            await SymbolTable.EvaluateAsync(p.FunctionDefinition, cancellationToken);
            return instance.Call(p.Name, Array.Empty<IMember>());
        }
    }
}
