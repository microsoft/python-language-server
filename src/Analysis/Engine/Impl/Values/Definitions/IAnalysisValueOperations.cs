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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    public interface IAnalysisValueOperations: IAnalysisValue {
        /// <summary>
        /// Attempts to call this object and returns the set of possible types it can return.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="args">The arguments being passed to the function</param>
        /// <param name="keywordArgNames">Keyword argument names, * and ** are included in here for splatting calls</param>
        IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames);

        /// <summary>
        /// Gets an attribute that's only declared in the classes dictionary, not in an instance
        /// dictionary
        /// </summary>
        IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name);

        void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value);
        void DeleteMember(Node node, AnalysisUnit unit, string name);
        void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value);
        IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs);

        IAnalysisSet CallReverseBinaryOp(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs);

        /// <summary>
        /// Provides implementation of __r*__methods (__radd__, __rsub__, etc...)
        /// 
        /// This is dispatched to when the LHS doesn't understand the RHS.  Unlike normal Python it's currently
        /// the LHS responsibility to dispatch to this.
        /// </summary>
        IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs);

        IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation);

        /// <summary>
        /// Returns the length of the object if it's known, or null if it's not a fixed size object.
        /// </summary>
        /// <returns></returns>
        int? GetLength();

        IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit);
        IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit);
        IAnalysisSet GetIterator(Node node, AnalysisUnit unit);
        IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit);
        IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index);
        void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value);
        IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit);
        IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context);
        IAnalysisSet Await(Node node, AnalysisUnit unit);
        IAnalysisSet GetReturnForYieldFrom(Node node, AnalysisUnit unit);

        /// <summary>
        /// If required, returns the resolved version of this value. If there is nothing
        /// to resolve, returns <c>this</c>.
        /// </summary>
        IAnalysisSet Resolve(AnalysisUnit unit);
    }
}
