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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    public interface IArgument {
        string Name { get; }
        Expression Expression { get; }
        object Value { get; }
    }

    public interface IListArgument {
        string Name { get; }
        IReadOnlyList<Expression> Expressions { get; }
        IReadOnlyList<IMember> Values { get; }
    }

    public interface IDictionaryArgument {
        string Name { get; }
        IReadOnlyDictionary<string, IMember> Arguments { get; }
        IReadOnlyDictionary<string, Expression> Expressions { get; }
    }

    public interface IArgumentSet {
        IReadOnlyList<IArgument> Arguments { get; }
        IListArgument ListArgument { get; }
        IDictionaryArgument DictionaryArgument { get; }
        int OverloadIndex { get; }
    }
}
