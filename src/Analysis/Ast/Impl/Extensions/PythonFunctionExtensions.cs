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

namespace Microsoft.Python.Analysis.Extensions {
    public static class PythonFunctionExtensions {
        public static bool IsUnbound(this IPythonFunction f) 
            => f.DeclaringType != null && f.MemberType == PythonMemberType.Function;

        public static bool IsBound(this IPythonFunction f) 
            => f.DeclaringType != null && f.MemberType == PythonMemberType.Method;

        public static bool HasClassFirstArgument(this IPythonClassMember m)
            => (m is IPythonFunction f && !f.IsStatic && (f.IsClassMethod || f.IsBound())) ||
               (m is IPythonProperty prop && !prop.IsStatic);
    }
}
