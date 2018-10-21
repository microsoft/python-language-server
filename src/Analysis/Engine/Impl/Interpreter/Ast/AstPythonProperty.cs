﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonProperty : IBuiltinProperty, ILocatedMember {
        private IPythonFunctionOverload _getter;

        public AstPythonProperty(
            PythonAst ast,
            string name,
            string documentation,
            ILocationInfo location
        ) {
            Documentation = documentation;
            IsReadOnly = true;
            Locations = new[] { location };
            Name = name;
        }

        public void AddOverload(IPythonFunctionOverload overload)
            => _getter = _getter ?? overload;

        public void MakeSettable() => IsReadOnly = false;

        public string Name { get; }
        public IPythonType Type => _getter?.ReturnType.FirstOrDefault();

        public bool IsStatic => false;
        public bool IsClassMethod  => false;

        public string Documentation { get; }

        public string Description => Type == null ? "property of unknown type" : "property of type {0}".FormatUI(Type.Name);

        public PythonMemberType MemberType => PythonMemberType.Property;
        
        public bool IsReadOnly { get; private set; }

        public IEnumerable<ILocationInfo> Locations { get; }
    }
}
