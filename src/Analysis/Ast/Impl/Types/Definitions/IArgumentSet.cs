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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Function call argument.
    /// </summary>
    public interface IArgument {
        /// <summary>
        /// Argument name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Expression that evaluates to the value of the argument.
        /// Function call parameter.
        /// </summary>
        Expression Expression { get; }

        /// <summary>
        /// Location of the argument in the function definition.
        /// Not the same as location of the <see cref="Expression"/>.
        /// </summary>
        LocationInfo Location { get; }

        /// <summary>
        /// Value of the argument.
        /// </summary>
        object Value { get; }
    }

    /// <summary>
    /// List argument, such as *args.
    /// </summary>
    public interface IListArgument {
        /// <summary>
        /// Argument name.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Expression that evaluates to the value of the argument.
        /// Function call parameter.
        /// </summary>
        Expression Expression { get; }

        /// <summary>
        /// Location of the argument in the function definition.
        /// Not the same as location of the <see cref="Expression"/>.
        /// </summary>
        LocationInfo Location { get; }

        /// <summary>
        /// Expressions that evaluate to the elements of the list.
        /// </summary>
        IReadOnlyList<Expression> Expressions { get; }

        /// <summary>
        /// Values of the elements of the list.
        /// </summary>
        IReadOnlyList<IMember> Values { get; }
    }

    /// <summary>
    /// Dictionary argument, such as **kwargs.
    /// </summary>
    public interface IDictionaryArgument {
        /// <summary>
        /// Argument name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Expression that evaluates to the value of the argument.
        /// Function call parameter.
        /// </summary>
        Expression Expression { get; }

        /// <summary>
        /// Location of the argument in the function definition.
        /// Not the same as location of the <see cref="Expression"/>.
        /// </summary>
        LocationInfo Location { get; }

        /// <summary>
        /// Dictionary arguments.
        /// </summary>
        IReadOnlyDictionary<string, IMember> Arguments { get; }

        /// <summary>
        /// Expressions that evaluate to arguments.
        /// Function call parameters.
        /// </summary>
        IReadOnlyDictionary<string, Expression> Expressions { get; }
    }

    /// <summary>
    /// Describes set of arguments for a function call.
    /// </summary>
    public interface IArgumentSet {
        /// <summary>
        /// Regular arguments
        /// </summary>
        IReadOnlyList<IArgument> Arguments { get; }

        /// <summary>
        /// List argument, such as *args.
        /// </summary>
        IListArgument ListArgument { get; }

        /// <summary>
        /// Dictionary argument, such as **kwargs.
        /// </summary>
        IDictionaryArgument DictionaryArgument { get; }

        /// <summary>
        /// Specifies which function overload to call.
        /// </summary>
        int OverloadIndex { get; }

        /// <summary>
        /// Evaluator associated with the set.
        /// </summary>
        IExpressionEvaluator Eval { get; }
    }
}
