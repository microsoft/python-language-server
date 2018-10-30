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

using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    static class ErrorMessages {
        public static string NotCallableCode { get; } = "not-callable";
        public static string NotCallable(string target) => string.IsNullOrEmpty(target) ?
            Resources.ErrorNotCallableEmpty :
            Resources.ErrorNotCallable.FormatUI(target);

        public static string UseBeforeDefCode { get; } = "use-before-def";
        public static string UseBeforeDef(string name) => Resources.ErrorUseBeforeDef.FormatUI(name);

        public static string UnresolvedImportCode { get; } = "unresolved-import";
        public static string UnresolvedImport(string name) => Resources.ErrorUnresolvedImport.FormatUI(name);
    }
}
