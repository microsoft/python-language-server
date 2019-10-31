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
using System.Linq;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class PythonSuperType : PythonType, IPythonSuperType {
        /// <summary>
        /// more info at https://docs.python.org/3/library/functions.html#super
        /// </summary>
        /// <param name="location"></param>
        /// <param name="mro">Should be a list of IPythonType</param>
        public PythonSuperType(IReadOnlyList<IPythonType> mro)
            : base("super", new Location(), string.Empty, BuiltinTypeId.Type) {
            Mro = mro;
        }

        public override string QualifiedName => $":SuperType[{string.Join(",", Mro.Select(t => t.QualifiedName))}]";

        public IReadOnlyList<IPythonType> Mro { get; }

        public override IMember GetMember(string name) => Mro.MaybeEnumerate().Select(c => c.GetMember(name)).ExcludeDefault().FirstOrDefault();

        public override IEnumerable<string> GetMemberNames() => Mro.MaybeEnumerate().SelectMany(cls => cls.GetMemberNames()).Distinct();


        /// <summary>
        ///  This will return PythonSuperType with a mro list that starts with the next class in line in classType's mro, 
        ///  or it will search classType's mro for typeToFild then build an mro list for the remaining classses after typeToFind.
        /// </summary>
        /// <param name="classType"></param>
        /// <param name="typeToFind"></param>
        /// <returns></returns>
        internal static PythonSuperType CreateSuper(IPythonClassType classType, IPythonType typeToFind = null) {
            var mro = classType?.Mro ?? Array.Empty<IPythonType>();
            if (mro.Count == 0) {
                return null;
            }

            // skip doing work if the newStartType is the first element in the callers mro
            if (typeToFind?.Equals(classType.Mro.FirstOrDefault()) == false) {
                var mroList = classType.Mro.ToList();
                var start = mroList.FindIndex(0, t => t.Equals(typeToFind));
                if (start >= 0) {
                    mro = mroList.GetRange(start, mro.Count - start).ToArray();
                } else {
                    return null;  // typeToFind wasn't in the mro
                }
            }

            var nextClassInLine = mro?.FirstOrDefault();
            if (nextClassInLine != null) {
                // Skip the first element, super's search starts at the next elemement in the mro for both super() and super(cls, typeToFind)
                return new PythonSuperType(mro.Skip(1).ToArray());
            }

            return null;
        }
    }
}

