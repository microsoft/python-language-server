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

namespace Microsoft.Python.Analysis.Diagnostics {
    public static class ErrorCodes {
        public const string TooManyFunctionArguments = "too-many-function-arguments";
        public const string TooManyPositionalArgumentsBeforeStar = "too-many-positional-arguments-before-star";
        public const string PositionalArgumentAfterKeyword = "positional-argument-after-keyword";
        public const string UnknownParameterName = "unknown-parameter-name";
        public const string ParameterAlreadySpecified = "parameter-already-specified";
        public const string ParameterMissing = "parameter-missing";
        public const string UnresolvedImport = "unresolved-import";
        public const string UndefinedVariable = "undefined-variable";
        public const string VariableNotDefinedGlobally= "variable-not-defined-globally";
        public const string VariableNotDefinedNonLocal = "variable-not-defined-nonlocal";
        public const string TypeVarLint = "typevar-linter";
    }
}
