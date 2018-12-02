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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Represents a file which is capable of being analyzed.  Can be cast to other project entry types
    /// for more functionality.  See also IPythonProjectEntry and IXamlProjectEntry.
    /// </summary>
    public interface IProjectEntry : IAnalyzable, IVersioned, IDisposable {
        /// <summary>
        /// Returns true if the project entry has been parsed and analyzed.
        /// </summary>
        bool IsAnalyzed { get; }

        /// <summary>
        /// Returns the project entries file path.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Returns the document unique identifier
        /// </summary>
        Uri DocumentUri { get; }

        /// <summary>
        /// Provides storage of arbitrary properties associated with the project entry.
        /// </summary>
        Dictionary<object, object> Properties { get; }

        IModuleContext AnalysisContext { get; }

        /// <summary>
        /// Document object corresponding to the entry.
        /// Can be null for entries that are not user documents
        /// such as modules.
        /// </summary>
        IDocument Document { get; }
    }
}
