using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Interpreter {
    public interface ICallable : IMember {
        /// <summary>
        /// Documentation for the function or property.
        /// </summary>
        string Documentation { get; }

        /// <summary>
        /// A user readable description of the function or property.
        /// </summary>
        string Description { get; }

        bool IsStatic { get; }
        bool IsClassMethod { get; }
    }
}
