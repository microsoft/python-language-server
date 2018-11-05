using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonProperty : IBuiltinProperty2, ILocatedMember {
        private IPythonFunctionOverload _getter;

        public AstPythonProperty(
            PythonAst ast,
            FunctionDefinition definition,
            ILocationInfo location
        ) {
            IsReadOnly = true;
            Locations = new[] { location };
            FunctionDefinition = definition;
        }

        public FunctionDefinition FunctionDefinition { get; }

        public void AddOverload(IPythonFunctionOverload overload)
            => _getter = _getter ?? overload;

        public void MakeSettable() => IsReadOnly = false;

        public IPythonType Type => _getter?.ReturnType.FirstOrDefault();

        public bool IsStatic => false;

        public string Documentation => FunctionDefinition.Documentation;

        public string Description => Type == null ? "property of unknown type" : "property of type {0}".FormatUI(Type.Name);

        public PythonMemberType MemberType => PythonMemberType.Property;
        
        public bool IsReadOnly { get; private set; }

        public IEnumerable<ILocationInfo> Locations { get; }
    }
}
