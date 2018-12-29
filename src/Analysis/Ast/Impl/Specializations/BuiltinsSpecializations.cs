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

using System;
using System.Collections.Generic;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations {
    public static class BuiltinsSpecializations {
        public static Func<IReadOnlyList<IMember>, IMember> Identity
            => (args => args.Count > 0 ? args[0] : null);

        public static Func<IReadOnlyList<IMember>, IMember> TypeInfo
            => (args => args.Count > 0 ? args[0].GetPythonType() : null);

        public static Func<IReadOnlyList<IMember>, IMember> Iterator
            => (args => args.Count > 0 && args[0] is IPythonSequence seq ? seq.GetIterator(): null);

        public static Func<IReadOnlyList<IMember>, IMember> Next
            => (args => args.Count > 0 && args[0] is IPythonIterator it ? it.Next : null);
    }
}
