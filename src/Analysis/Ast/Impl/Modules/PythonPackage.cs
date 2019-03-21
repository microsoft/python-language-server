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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Module-scoped representation of the module or implicit package
    /// Contains either module members, members + imported children of explicit package or imported implicit package children
    /// Instance is unique for each module analysis
    /// </summary>
    internal sealed class PythonVariableModule : LocatedMember, IPythonModule, IEquatable<IPythonModule> {
        private readonly Dictionary<string, PythonVariableModule> _children = new Dictionary<string, PythonVariableModule>();

        public string Name { get; }
        public IPythonModule Module { get; }
        public IPythonInterpreter Interpreter { get; }

        public IDocumentAnalysis Analysis => Module?.Analysis;
        public string Documentation => Module?.Documentation ?? string.Empty;
        public string FilePath => Module?.FilePath;
        public bool IsBuiltin => true;
        public bool IsAbstract => false;
        public bool IsSpecialized => Module?.IsSpecialized ?? false;
        public ModuleType ModuleType => Module?.ModuleType ?? ModuleType.Package;
        public IPythonModule PrimaryModule => null;
        public IPythonModule Stub => null;
        public IGlobalScope GlobalScope => Module?.GlobalScope;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public Uri Uri => Module?.Uri;

        public PythonVariableModule(string name, IPythonInterpreter interpreter): base(PythonMemberType.Module) {
            Name = name;
            Interpreter = interpreter;
        }

        public PythonVariableModule(IPythonModule module): base(module) {
            Name = module.Name;
            Interpreter = module.Interpreter;
            Module = module;
        }

        public void AddChildModule(string memberName, PythonVariableModule module) => _children[memberName] = module;

        public IMember GetMember(string name) => Module?.GetMember(name) ?? (_children.TryGetValue(name, out var module) ? module : default);
        public IEnumerable<string> GetMemberNames() => Module != null ? Module.GetMemberNames().Concat(_children.Keys) : _children.Keys;

        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => GetMember(memberName);
        public IMember Index(IPythonInstance instance, object index) => Interpreter.UnknownType;
        public IMember CreateInstance(string typeName = null, IArgumentSet args = null) => this;

        public bool Equals(IPythonModule other) => other is PythonVariableModule module && Name.EqualsOrdinal(module.Name);

        public Task LoadAndAnalyzeAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Can't analyze analysis-only type");
    }
}
