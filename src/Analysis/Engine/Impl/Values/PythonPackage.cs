// Python Tools for Visual Studio
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
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Package representation inside python file
    /// </summary>
    internal sealed class PythonPackage : AnalysisValue, IModule {
        private readonly PythonAnalyzer _projectState;
        private readonly Dictionary<string, IAnalysisSet> _childModules;

        public PythonPackage(string name, PythonAnalyzer projectState) {
            Name = name;
            _projectState = projectState;
            _childModules = new Dictionary<string, IAnalysisSet>();
        }

        public override string ShortDescription => Name;
        public override string Documentation => Name;
        public override string Description => Name;
        public override string Name { get; }
        public override IPythonType PythonType => _projectState.Types[BuiltinTypeId.Module];
        public override PythonMemberType MemberType => PythonMemberType.Module;
        public override BuiltinTypeId TypeId => BuiltinTypeId.Module;

        public IPythonProjectEntry ProjectEntry => null;
        public IScope Scope => null;

        public void AddChildModule(string memberName, IAnalysisSet module) {
            if (_childModules.TryGetValue(memberName, out var existing)) {
                _childModules[memberName] = existing.Add((AnalysisValue)module);
            } else {
                _childModules[memberName] = module;
            }
        }

        public IModule GetChildPackage(IModuleContext context, string name) 
            => _childModules.TryGetValue(name, out var child) ? GetModule(child) : null;

        public IEnumerable<KeyValuePair<string, AnalysisValue>> GetChildrenPackages(IModuleContext context) {
            return _childModules.Select(kvp => new KeyValuePair<string, AnalysisValue>(kvp.Key, (AnalysisValue)GetModule(kvp.Value)));
        }

        public IEnumerable<string> GetModuleMemberNames(IModuleContext context) => _childModules.Keys;

        public void Imported(AnalysisUnit unit) {}

        public void SpecializeFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis) {}

        public IAnalysisSet GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef, IScope linkedScope, string linkedName) => GetMember(node, unit, name);

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var analysisSet = base.GetMember(node, unit, name);
            if (_childModules.TryGetValue(name, out var child)) {
                analysisSet = analysisSet.Union(child);
            }
            return analysisSet;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None)
            => new Dictionary<string, IAnalysisSet>(_childModules);

        private static IModule GetModule(IAnalysisSet value) 
            => value is IModule module ? module : MultipleMemberInfo.Create(value.Where(m => m is IModule)) as IModule;
    }
}
