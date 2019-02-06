﻿// Copyright(c) Microsoft Corporation
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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis {
    public static class VariableExtensions {
        public static T GetValue<T>(this IVariable v) where T : class => v.Value as T;

        public static bool IsTypeInfo(this IVariable v) => v.Value is IPythonType;
        public static bool IsTypeInfoOf<T>(this IVariable v) where T : class, IPythonType => v.Value is T;

        public static bool IsInstance(this IVariable v) => v.Value is IPythonInstance;
        public static bool IsInstanceOf<T>(this IVariable v) where T: class, IPythonInstance => v.Value is T;
    }
}
