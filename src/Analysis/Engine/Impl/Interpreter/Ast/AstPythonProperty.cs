using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonProperty : AstPythonFunction, IPythonProperty {
        private IPythonFunctionOverload _getter;

        public AstPythonProperty(FunctionDefinition fd, IPythonModule declaringModule, IPythonType declaringType, ILocationInfo location)
            : base(fd, declaringModule, declaringType, location) {
        }

        #region IMember
        public override PythonMemberType MemberType => PythonMemberType.Property;
        #endregion

        #region IPythonFunction
        public override bool IsStatic => false;
        #endregion

        #region IPythonProperty
        public string Description => Type == null ? Resources.PropertyOfUnknownType : Resources.PropertyOfType.FormatUI(Type.Name);
        #endregion

        internal override void AddOverload(IPythonFunctionOverload overload) => _getter = _getter ?? overload;

        public void MakeSettable() => IsReadOnly = false;

        public IPythonType Type => _getter?.ReturnType.FirstOrDefault();

        public bool IsReadOnly { get; private set; } = true;
    }
}
