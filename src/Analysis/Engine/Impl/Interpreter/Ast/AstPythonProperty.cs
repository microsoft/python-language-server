using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonProperty : AstPythonType, IPythonProperty {
        private IPythonFunctionOverload _getter;

        public AstPythonProperty(FunctionDefinition fd, IPythonModule declaringModule, IPythonType declaringType, ILocationInfo location)
            : base(fd.Name, declaringModule, null, location) {
            FunctionDefinition = fd;
            DeclaringType = declaringType;
        }

        #region IMember
        public override PythonMemberType MemberType => PythonMemberType.Property;
        #endregion

        #region IPythonProperty
        public bool IsStatic => false;
        public IPythonType DeclaringType { get; }
        public string Description 
            => Type == null ? Resources.PropertyOfUnknownType : Resources.PropertyOfType.FormatUI(Type.Name);
        public FunctionDefinition FunctionDefinition { get; }
        #endregion

        internal void AddOverload(IPythonFunctionOverload overload) => _getter = _getter ?? overload;

        public void MakeSettable() => IsReadOnly = false;

        public IPythonType Type => _getter?.ReturnType.FirstOrDefault();

        public bool IsReadOnly { get; private set; } = true;
    }
}
