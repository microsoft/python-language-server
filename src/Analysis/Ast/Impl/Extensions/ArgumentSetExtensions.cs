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
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis {
    public static class ArgumentSetExtensions {
        public static IReadOnlyList<T> Values<T>(this IArgumentSet args)
            => args.Arguments.Select(a => a.Value).OfType<T>().ToArray();

        public static IReadOnlyList<KeyValuePair<string, T>> Arguments<T>(this IArgumentSet args) where T : class
            => args.Arguments.Select(a => new KeyValuePair<string, T>(a.Name, a.Value as T)).ToArray();

        public static T Argument<T>(this IArgumentSet args, int index) where T : class
            => args.Arguments[index].Value as T;
    }
}
