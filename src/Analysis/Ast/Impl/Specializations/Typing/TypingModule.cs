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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Specializations.Typing {
    internal sealed class TypingModule : SpecializedModule {
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();

        private TypingModule(string modulePath, IServiceContainer services) : base("typing", modulePath, services) { }

        public static async Task<IPythonModule> CreateAsync(IServiceContainer services, CancellationToken cancellationToken = default) {
            var interpreter = services.GetService<IPythonInterpreter>();
            var module = await interpreter.ModuleResolution
                .SpecializeModuleAsync("typing", modulePath => new TypingModule(modulePath, services), cancellationToken) as TypingModule;
            module?.SpecializeMembers();
            return module;
        }

        #region IMemberContainer
        public override IMember GetMember(string name) => _members.TryGetValue(name, out var m) ? m : null;
        public override IEnumerable<string> GetMemberNames() => _members.Keys;
        #endregion

        private void SpecializeMembers() {
            // TypeVar
            var fn = new PythonFunctionType("TypeVar", this, null, GetMemberDocumentation, GetMemberLocation);
            var o = new PythonFunctionOverload(fn.Name, this, _ => fn.Location);
            // When called, create generic parameter type. For documentation
            // use original TypeVar declaration so it appear as a tooltip.
            o.SetReturnValueProvider((module, overload, location, args) => GenericTypeParameter.FromTypeVar(args, module, location));

            fn.AddOverload(o);
            _members["TypeVar"] = fn;

            _members["Iterator"] = new GenericType("Iterator", this,
                (typeArgs, module, location) => TypingIteratorType.Create(module, typeArgs));

            _members["Iterable"] = new GenericType("Iterable", this,
                (typeArgs, module, location) => CreateList("Iterable", module, typeArgs, false));
            _members["Sequence"] = new GenericType("Sequence", this,
                (typeArgs, module, location) => CreateList("Sequence", module, typeArgs, false));
            _members["List"] = new GenericType("List", this,
                (typeArgs, module, location) => CreateList("List", module, typeArgs, false));

            _members["Tuple"] = new GenericType("Tuple", this,
                (typeArgs, module, location) => CreateCollectionType("Tuple", BuiltinTypeId.ListIterator, module, typeArgs.Count, typeArgs, false));

            _members["Mapping"] = new GenericType("Mapping", this,
                (typeArgs, module, location) => new TypingDictionaryType("Mapping", ));
            _members["Dict"] = new GenericType("Dict", this,
                (typeArgs, module, location) => TypingDictionaryType.Create(module, typeArgs));
        }


        private string GetMemberDocumentation(string name)
            => base.GetMember(name)?.GetPythonType()?.Documentation;
        private LocationInfo GetMemberLocation(string name)
            => (base.GetMember(name)?.GetPythonType() as ILocatedMember)?.Location ?? LocationInfo.Empty;

        private IPythonType CreateList(string typeName,  IPythonModule module, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 1) {
                return new TypingListType(typeName, BuiltinTypeId.ListIterator, module, typeArgs[0], isMutable);
            }
            // TODO: report wrong number of arguments
            return module.Interpreter.UnknownType;
        }
        private IPythonType CreateTuple(string typeName, IPythonModule module, IReadOnlyList<IPythonType> typeArgs, bool isMutable) {
            if (typeArgs.Count == 1) {
                return new TypingTupleType(typeName, BuiltinTypeId.ListIterator, module, typeArgs[0], isMutable);
            }
            // TODO: report wrong number of arguments
            return module.Interpreter.UnknownType;
        }
    }
}
