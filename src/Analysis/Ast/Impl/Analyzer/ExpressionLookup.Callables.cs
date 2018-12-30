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

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class ExpressionLookup {
        private async Task<IMember> GetValueFromCallableAsync(CallExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            var target = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            IMember value = null;
            switch (target) {
                case IPythonFunctionType fnt:
                    value = await GetValueFromFunctionAsync(fnt, null, expr, cancellationToken);
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
                return await GetValueFromFunctionAsync(pif, pi, expr, cancellationToken);
            }

            // Try using __call__
            var call = type.GetMember("__call__").GetPythonType<IPythonFunctionType>();
            if (call != null) {
                return await GetValueFromFunctionAsync(call, pi, expr, cancellationToken);
            }

            return null;
        }

        private Task<IMember> GetValueFromFunctionAsync(IPythonFunction fn, CallExpression expr, CancellationToken cancellationToken = default)
            => GetValueFromFunctionAsync(fn.GetPythonType<IPythonFunctionType>(), fn.Self, expr, cancellationToken);

        private async Task<IMember> GetValueFromFunctionAsync(IPythonFunctionType fn, IPythonInstance instance, CallExpression expr, CancellationToken cancellationToken = default) {
            // Determine argument types
            var args = new List<IMember>();
            // For static and regular methods add 'self' or 'cls'
            if (fn.HasClassFirstArgument()) {
                args.Add(fn.IsClassMethod ? fn.DeclaringType : (IMember)instance);
            }

            foreach (var a in expr.Args.MaybeEnumerate()) {
                var type = await GetValueFromExpressionAsync(a.Expression, cancellationToken);
                args.Add(type ?? UnknownType);
            }

            return await GetValueFromFunctionAsync(fn, args, cancellationToken);
        }

        private async Task<IMember> GetValueFromFunctionAsync(IPythonFunctionType fn, IReadOnlyList<IMember> args, CancellationToken cancellationToken = default) {
            IMember value = null;
            var overload = FindOverload(fn, args);
            if (overload != null) {
                // TODO: provide instance
                value = GetFunctionReturnValue(overload, null, args);
                if (value.IsUnknown() && overload.FunctionDefinition != null) {
                    // Function may not have been walked yet. Do it now.
                    if (await FunctionWalkers.ProcessFunctionAsync(overload.FunctionDefinition, cancellationToken)) {
                        value = GetFunctionReturnValue(overload, null, args);
                    }
                }
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

        private IMember GetFunctionReturnValue(IPythonFunctionOverload o, IPythonInstance instance, IReadOnlyList<IMember> args)
            => o?.GetReturnValue(instance, args) ?? UnknownType;

        private async Task<IPythonType> GetPropertyReturnTypeAsync(IPythonPropertyType p, Expression expr, CancellationToken cancellationToken = default) {
            if (p.Type.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                await FunctionWalkers.ProcessFunctionAsync(p.FunctionDefinition, cancellationToken);
            }
            return p.Type ?? UnknownType;
        }
    }
}
