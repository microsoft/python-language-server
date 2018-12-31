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
// permissions and limitations under the License.    }

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Utilities {
    internal static class CodeFormatter {
        public static string FormatSequence(string sequenceName, IEnumerable<IPythonType> types, char openBrace)
            => FormatSequence(sequenceName, types.Select(t => t.Name), openBrace);

        public static string FormatSequence(string sequenceName, IEnumerable<string> names, char openBrace) {
            var sb = new StringBuilder(sequenceName);
            sb.Append(openBrace);
            var i = 0;
            foreach (var name in names) {
                sb.AppendIf(i > 0, ", ");
                sb.Append(name);
                i++;
            }
            sb.AppendIf(openBrace == '[', ']');
            sb.AppendIf(openBrace == '(', ')');
            return sb.ToString();
        }
    }
}
