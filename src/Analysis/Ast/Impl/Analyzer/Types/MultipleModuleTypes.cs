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

namespace Microsoft.Python.Analysis.Analyzer.Types {
    internal sealed class MultipleModuleTypes : PythonMultipleTypes, IPythonModule {
        public MultipleModuleTypes(IPythonType[] members) : base(members) { }

        private IEnumerable<IPythonModule> Modules => Types.OfType<IPythonModule>();

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Module;
        #endregion

        #region IMemberContainer
        public override IPythonType GetMember(string name) => Create(Modules.Select(m => m.GetMember(name)));
        public override IEnumerable<string> GetMemberNames() => Modules.SelectMany(m => m.GetMemberNames()).Distinct();
        #endregion

        #region IPythonType
        public override string Name => ChooseName(Modules.Select(m => m.Name)) ?? "<module>";
        public override string Documentation => ChooseDocumentation(Modules.Select(m => m.Documentation));
        public override IPythonModule DeclaringModule => null;
        public override BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public override bool IsBuiltin => true;
        #endregion

        #region IPythonModule
        public IEnumerable<string> GetChildrenModuleNames() => Modules.SelectMany(m => m.GetChildrenModuleNames());
        public void Load() {
            List<Exception> exceptions = null;
            foreach (var m in Modules) {
                try {
                    m.Load();
                } catch (Exception ex) {
                    exceptions = exceptions ?? new List<Exception>();
                    exceptions.Add(ex);
                }
            }
            if (exceptions != null) {
                throw new AggregateException(exceptions);
            }
        }
        public IEnumerable<string> ParseErrors { get; private set; } = Enumerable.Empty<string>();
        #endregion

        #region IPythonFile
        public string FilePath => null;
        public Uri Uri => null;
        public IPythonInterpreter Interpreter => null;
        #endregion

        public void Dispose() { }
    }
}
