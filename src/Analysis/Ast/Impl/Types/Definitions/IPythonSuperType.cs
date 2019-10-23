using System;
using System.Collections.Generic;


namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents Python class type definition.
    /// </summary>
    public interface IPythonSuperType  {
        /// <summary>
        /// Python Method Resolution Order (MRO).
        /// </summary>
        IReadOnlyList<IPythonType> Mro { get; }

    }
}
