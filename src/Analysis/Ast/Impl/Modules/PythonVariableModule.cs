﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Module-scoped representation of the module or implicit package
    /// Contains either module members, members + imported children of explicit package or imported implicit package children
    /// Instance is unique for each module analysis
    /// </summary>
    internal sealed class PythonVariableModule : LocatedMember, IPythonModule, IEquatable<IPythonModule>, ILocationConverter {
        private readonly Dictionary<string, PythonVariableModule> _children = new Dictionary<string, PythonVariableModule>();
 
        public string Name { get; }
        public string QualifiedName => Name;

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
        public IPythonModule Stub => Module?.Stub;
        public IGlobalScope GlobalScope => Module?.GlobalScope;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public Uri Uri => Module?.Uri;
        public override PythonMemberType MemberType => PythonMemberType.Module;
        public bool IsTypeshed => Module?.IsTypeshed == true;
        public ModuleState ModuleState => Module?.ModuleState ?? ModuleState.None;
        public IEnumerable<string> ChildrenNames => _children.Keys;

        public PythonVariableModule(string name, IPythonInterpreter interpreter) : base(null) { 
            Name = name;
            Interpreter = interpreter;
            SetDeclaringModule(this);
        }

        public PythonVariableModule(IPythonModule module): base(module) { 
            Name = module.Name;
            Interpreter = module.Interpreter;
            Module = module;
        }

        public void AddChildModule(string memberName, PythonVariableModule module) => _children[memberName] = module;

        public IMember GetMember(string name) => _children.TryGetValue(name, out var module) ? module : Module?.GetMember(name);
        public IEnumerable<string> GetMemberNames() => Module != null ? Module.GetMemberNames().Concat(ChildrenNames).Distinct() : ChildrenNames;

        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => GetMember(memberName);
        public IMember Index(IPythonInstance instance, IArgumentSet args) => Interpreter.UnknownType;
        public IMember CreateInstance(IArgumentSet args = null) => new PythonInstance(this);

        public bool Equals(IPythonModule other) => other is PythonVariableModule module && Name.EqualsOrdinal(module.Name);

        public override bool Equals(object obj) => Equals(obj as IPythonModule);
        public override int GetHashCode() => Name.GetHashCode();

        #region ILocationConverter
        public SourceLocation IndexToLocation(int index) => (Module as ILocationConverter)?.IndexToLocation(index) ?? default;
        public int LocationToIndex(SourceLocation location) => (Module as ILocationConverter)?.LocationToIndex(location) ?? default;
        #endregion
    }
}
