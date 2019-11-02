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
        /// Implements 'super' specialization type.
        /// See also https://docs.python.org/3/library/functions.html#super
        /// </summary>
        /// <param name="mro">The derived class MRO.</param>
        /// <param name="declaringModule">Declaring module.</param>
        public PythonSuperType(IReadOnlyList<IPythonType> mro, IPythonModule declaringModule)
            : base("super", new Location(declaringModule), string.Empty, BuiltinTypeId.Type) {
            Mro = mro;
        }

        public override string QualifiedName => $":SuperType[{string.Join(",", Mro.Select(t => t.QualifiedName))}]";

        public IReadOnlyList<IPythonType> Mro { get; }

        public override IMember GetMember(string name)
            => Mro.MaybeEnumerate().Select(c => c.GetMember(name)).ExcludeDefault().FirstOrDefault();

        public override IEnumerable<string> GetMemberNames() 
            => Mro.MaybeEnumerate().SelectMany(cls => cls.GetMemberNames()).Distinct();

        /// <summary>
        /// Creates PythonSuperType with a MRO list that starts with the next class in line in classType's MRO, 
        /// or searches classType's MRO for <see cref="typeToFind"/>, then builds an MRO list for
        /// the remaining classes after <see cref="typeToFind"/>.
        /// </summary>
        /// <param name="classType"></param>
        /// <param name="typeToFind"></param>
        /// <returns></returns>
        internal static PythonSuperType Create(IPythonClassType classType, IPythonType typeToFind = null) {
            var mro = classType?.Mro ?? Array.Empty<IPythonType>();
            if (classType == null || mro.Count == 0) {
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
            // Skip the first element, super's search starts at the next element in the mro for both super() and super(cls, typeToFind)
            return nextClassInLine != null ? new PythonSuperType(mro.Skip(1).ToArray(), classType.DeclaringModule) : null;
        }
    }
}

