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
                case IPythonInstance pi:
                    value = await GetValueFromInstanceCall(pi, expr, cancellationToken);
                    break;
                case IPythonFunction pf:
                    value = await GetValueFromFunctionAsync(pf, expr, cancellationToken);
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
            if (type is IPythonFunction pif) {
                return await GetValueFromFunctionAsync(pif, expr, cancellationToken);
            }

            // Try using __call__
            if (type.GetMember("__call__") is IPythonFunction call) {
                return await GetValueFromFunctionAsync(call, expr, cancellationToken);
            }

            return null;
        }

        private async Task<IMember> GetValueFromFunctionAsync(IPythonFunction fn, Expression expr, CancellationToken cancellationToken = default) {
            if (!(expr is CallExpression callExpr)) {
                Debug.Assert(false, "Call to GetValueFromFunctionAsync with non-call expression.");
                return null;
            }

            // Determine argument types
            var args = new List<IMember>();
            // For static and regular methods add 'self' or 'cls'
            if (fn.HasClassFirstArgument()) {
                // TODO: tell between static and regular by passing instance and not the class type info.
                args.Add(fn.DeclaringType);
            }

            foreach (var a in callExpr.Args.MaybeEnumerate()) {
                var type = await GetValueFromExpressionAsync(a.Expression, cancellationToken);
                args.Add(type ?? UnknownType);
            }

            IMember value = null;

            var overload = FindOverload(fn, args);
            if (overload != null) {
                // TODO: provide instance
                value = GetFunctionReturnValue(overload, null, args);
                if (value.IsUnknown() && fn.FunctionDefinition != null) {
                    // Function may not have been walked yet. Do it now.
                    await FunctionWalkers.ProcessFunctionAsync(fn.FunctionDefinition, cancellationToken);
                    value = GetFunctionReturnValue(overload, null, args);
                }
            }

            return value ?? UnknownType;
        }

        private IPythonFunctionOverload FindOverload(IPythonFunction fn, ICollection<IMember> args) {
            // Find best overload match. Of only one, use it.
            // TODO: match better, see ArgumentSet class in DDG.
            IPythonFunctionOverload overload = null;
            if (fn.Overloads.Count == 1) {
                overload = fn.Overloads[0];
            } else {
                // Try exact match
                overload = fn.Overloads.FirstOrDefault(o => o.Parameters.Count == args.Count);

                overload = overload ?? fn.Overloads
                               .Where(o => o.Parameters.Count >= args.Count)
                               .FirstOrDefault(o => {
                                   // Match so overall param count is bigger, but required params
                                   // count is less or equal to the passed arguments.
                                   var requiredParams = o.Parameters.Where(p => string.IsNullOrEmpty(p.DefaultValueString)).ToArray();
                                   return requiredParams.Length <= args.Count;
                               });
            }
            return overload;
        }

        private IMember GetFunctionReturnValue(IPythonFunctionOverload o, IPythonInstance instance, IReadOnlyList<IMember> args)
            => o?.GetReturnValue(instance, args) ?? UnknownType;

        private async Task<IPythonType> GetPropertyReturnTypeAsync(IPythonProperty p, Expression expr, CancellationToken cancellationToken = default) {
            if (p.Type.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                await FunctionWalkers.ProcessFunctionAsync(p.FunctionDefinition, cancellationToken);
            }

            return p.Type ?? UnknownType;
        }
    }
}
