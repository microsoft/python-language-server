﻿// Python Tools for Visual Studio
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

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public interface IModuleAnalysis {
        IModuleContext InterpreterContext { get; }
        PythonAnalyzer ProjectState { get; }
        IScope Scope { get; }
        string ModuleName { get; }

        /// <summary>
        /// Evaluates the given expression in at the provided line number and returns the values
        /// that the expression can evaluate to.
        /// </summary>
        /// <param name="exprText">The expression to determine the result of.</param>
        /// <param name="location">The location in the file where the expression should be evaluated.</param>
        IEnumerable<AnalysisValue> GetValues(string exprText, SourceLocation location);
        IEnumerable<AnalysisValue> GetValues(Expression expr, SourceLocation location, IScope scope = null);

        /// <summary>
        /// Gets the variables the given expression evaluates to.  Variables
        /// include parameters, locals, and fields assigned on classes, modules
        /// and instances.
        /// 
        /// Variables are classified as either definitions or references.  Only
        /// parameters have unique definition points - all other types of
        /// variables have only one or more references.
        /// </summary>
        /// <param name="exprText">The expression to find variables for.</param>
        /// <param name="location">
        /// The location in the file where the expression should be evaluated.
        /// </param>
        IEnumerable<IAnalysisVariable> GetVariables(string exprText, SourceLocation location);
        VariablesResult GetVariables(Expression expr, SourceLocation location, string originalText = null, IScope scope = null);

        /// <summary>
        /// Evaluates a given expression and returns a list of members which
        /// exist in the expression.
        /// 
        /// If the expression is an empty string returns all available members
        /// at that location.
        /// </summary>
        /// <param name="exprText">The expression to find members for.</param>
        /// </param>
        /// <param name="location">
        /// The location in the file where the expression should be evaluated.
        /// </param>
        IEnumerable<MemberResult> GetMembers(string exprText, SourceLocation location, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults);

        IEnumerable<MemberResult> GetMembers(Expression expr, SourceLocation location, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults, IScope scope = null);

        /// <summary>
        /// Gets information about the available signatures for the given expression.
        /// </summary>
        /// <param name="exprText">The expression to get signatures for.</param>
        /// <param name="location">The location in the file.</param>
        IEnumerable<IOverloadResult> GetSignatures(string exprText, SourceLocation location);
        IEnumerable<IOverloadResult> GetSignatures(Expression expr, SourceLocation location, IScope scope = null);

        /// <summary>
        /// Gets the hierarchy of class and function definitions at the
        /// specified location.
        /// </summary>
        /// <param name="location">The location in the file.</param>
        IEnumerable<MemberResult> GetDefinitionTree(SourceLocation location);

        /// <summary>
        /// Gets information about methods defined on base classes but not
        /// directly on the current class.
        /// </summary>
        /// <param name="location">The location in the file.</param>
        IEnumerable<IOverloadResult> GetOverrideable(SourceLocation location);

        /// <summary>
        /// Gets the available names at the given location.  This includes
        /// built-in variables, global variables, and locals.
        /// </summary>
        /// <param name="location">
        /// The location in the file where the available members should be looked up.
        /// </param>
        IEnumerable<MemberResult> GetAllAvailableMembers(SourceLocation location, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults);

        /// <summary>
        /// Gets the AST for the given text as if it appeared at the specified location.
        /// 
        /// If the expression is a member expression such as "fob.__bar" and the
        /// line number is inside of a class definition this will return a
        /// MemberExpression with the mangled name like "fob._ClassName__bar".
        /// </summary>
        /// <param name="exprText">The expression to evaluate.</param>
        /// <param name="index">
        /// The 0-based index into the file where the expression should be evaluated.
        /// </param>
        PythonAst GetAstFromText(string exprText, SourceLocation location);

        IEnumerable<MemberResult> GetAllAvailableMembersFromScope(IScope scope, GetMemberOptions options);

        /// <summary>
        /// Given source location attempts to retrieve custom prefix for mangled method names.
        /// </summary>
        /// <remarks>See https://en.wikipedia.org/wiki/Name_mangling, Python section.</remarks>
        /// <param name="sourceLocation"></param>
        string GetPrivatePrefix(SourceLocation sourceLocation);
        }
}
