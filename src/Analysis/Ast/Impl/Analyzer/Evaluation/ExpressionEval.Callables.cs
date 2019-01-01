using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Extensions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        private async Task<IMember> GetValueFromCallableAsync(CallExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            var target = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            IMember value = null;
            switch (target) {
                case IPythonFunctionType fnt:
                    value = await GetValueFromFunctionTypeAsync(fnt, null, expr, cancellationToken);
                    break;
                case IPythonFunction fn:
                    value = await GetValueFromFunctionAsync(fn, expr, cancellationToken);
                    break;
                case IPythonIterator _:
                    value = target;
                    break;
                case IPythonInstance pi:
                    value = await GetValueFromInstanceCall(pi, expr, cancellationToken);
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

        private async Task<IMember> GetValueFromInstanceCall(IPythonInstance pi, CallExpression expr, CancellationToken cancellationToken = default) {
            // Call on an instance such as 'a = 1; a()'
            // If instance is a function (such as an unbound method), then invoke it.
            var type = pi.GetPythonType();
            if (type is IPythonFunctionType pif) {
                return await GetValueFromFunctionTypeAsync(pif, pi, expr, cancellationToken);
            }

            // Try using __call__
            var call = type.GetMember("__call__").GetPythonType<IPythonFunctionType>();
            if (call != null) {
                return await GetValueFromFunctionTypeAsync(call, pi, expr, cancellationToken);
            }

            return null;
        }

        private Task<IMember> GetValueFromFunctionAsync(IPythonFunction fn, CallExpression expr, CancellationToken cancellationToken = default)
            => GetValueFromFunctionTypeAsync(fn.GetPythonType<IPythonFunctionType>(), fn.Self, expr, cancellationToken);

        private async Task<IMember> GetValueFromFunctionTypeAsync(IPythonFunctionType fn, IPythonInstance instance, CallExpression expr, CancellationToken cancellationToken = default) {
            // Determine argument types
            var args = new List<IMember>();
            // For static and regular methods add 'self' or 'cls'
            if (fn.HasClassFirstArgument()) {
                args.Add(fn.IsClassMethod ? fn.DeclaringType : ((IMember)instance ?? Interpreter.UnknownType));
            }

            foreach (var a in expr.Args.MaybeEnumerate()) {
                var type = await GetValueFromExpressionAsync(a.Expression, cancellationToken);
                args.Add(type ?? UnknownType);
            }

            return await GetValueFromFunctionAsync(fn, expr, args, cancellationToken);
        }

        private async Task<IMember> GetValueFromFunctionAsync(IPythonFunctionType fn, Expression invokingExpression, IReadOnlyList<IMember> args, CancellationToken cancellationToken = default) {
            IMember value = null;
            // If order to be able to find matching overload, we need to know
            // parameter types and count. This requires function to be analyzed.
            // Since we don't know which overload we will need, we have to 
            // process all known overloads for the function.
            foreach (var o in fn.Overloads) {
                await FunctionWalkers.ProcessFunctionAsync(o.FunctionDefinition, cancellationToken);
            }
            // Now we can go and find overload with matching arguments.
            var overload = FindOverload(fn, args);
            if (overload != null) {
                var location = GetLoc(invokingExpression);
                value = GetFunctionReturnValue(overload, location, args);
            }
            return value ?? UnknownType;
        }

        private IPythonFunctionOverload FindOverload(IPythonFunctionType fn, IReadOnlyList<IMember> args) {
            // Find best overload match. Of only one, use it.
            // TODO: match better, see ArgumentSet class in DDG.
            if (fn.Overloads.Count == 1) {
                return fn.Overloads[0];
            }

            // Try match number of parameters
            var matching = fn.Overloads.Where(o => o.Parameters.Count == args.Count);
            var argTypes = args.Select(a => a.GetPythonType());
            var overload = matching.FirstOrDefault(o => {
                var paramTypes = o.Parameters.Select(p => p.Type);
                return paramTypes.SequenceEqual(argTypes);
            });

            if (overload != null) {
                return overload;
            }

            return fn.Overloads
                       .Where(o => o.Parameters.Count >= args.Count)
                       .FirstOrDefault(o => {
                           // Match so overall param count is bigger, but required params
                           // count is less or equal to the passed arguments.
                           var requiredParams = o.Parameters.Where(p => string.IsNullOrEmpty(p.DefaultValueString)).ToArray();
                           return requiredParams.Length <= args.Count;
                       });
        }

        private IMember GetFunctionReturnValue(IPythonFunctionOverload o, LocationInfo location, IReadOnlyList<IMember> args)
            => o?.GetReturnValue(location, args) ?? UnknownType;

        private async Task<IPythonType> GetPropertyReturnTypeAsync(IPythonPropertyType p, Expression expr, CancellationToken cancellationToken = default) {
            if (p.Type.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                await FunctionWalkers.ProcessFunctionAsync(p.FunctionDefinition, cancellationToken);
            }
            return p.Type ?? UnknownType;
        }
    }
}
