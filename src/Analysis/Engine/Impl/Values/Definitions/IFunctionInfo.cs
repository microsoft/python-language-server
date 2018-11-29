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

using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    public interface IFunctionInfo: IAnalysisValue {
        FunctionDefinition FunctionDefinition { get; }
        bool IsStatic { get; }
        bool IsClassMethod { get; }
        bool IsProperty { get; }
        bool IsClosure { get; }
        IPythonProjectEntry ProjectEntry { get; }
    }

    public interface IFunctionInfo2 : IFunctionInfo {
        bool IsAbstract { get; }
    }
}
