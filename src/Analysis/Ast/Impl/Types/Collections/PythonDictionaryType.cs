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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types.Collections {
    internal class PythonDictionaryType : PythonCollectionType {
        public PythonDictionaryType(IPythonInterpreter interpreter, bool isMutable = true)
            : base(null, BuiltinTypeId.Dict, interpreter, isMutable) {
        }

        public override IMember CreateInstance(string typeName, Node definition, IArgumentSet args) {
            var contents = args.Arguments.Count == 1
                ? args.Arguments[0].Value as IReadOnlyDictionary<IMember, IMember>
                : EmptyDictionary<IMember, IMember>.Instance;
            return new PythonDictionary(this, definition, contents);
        }

        // Constructor call
        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args)
            => CreateInstance(Name, instance?.Definition ?? LocationInfo.Empty, args);

        public override BuiltinTypeId TypeId => BuiltinTypeId.Dict;
        public override PythonMemberType MemberType => PythonMemberType.Class;
    }
}
