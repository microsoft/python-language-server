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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public interface IVariableDefinition: IReferenceable {
        bool VariableStillExists { get; }
        bool AddReference(Node node, AnalysisUnit unit);
        bool AddReference(EncodedLocation location, IVersioned module);
        bool AddAssignment(EncodedLocation location, IVersioned entry);
        bool AddAssignment(Node node, AnalysisUnit unit);
        bool IsAssigned { get; }
        bool HasTypes { get; }
        bool IsEphemeral { get; }

        /// <summary>
        /// Returns the set of types which currently are stored in the VariableDef.  The
        /// resulting set will not mutate in the future even if the types in the VariableDef
        /// change in the future.
        /// </summary>
        IAnalysisSet Types { get; }

        bool AddTypes(AnalysisUnit unit, IAnalysisSet newTypes, bool enqueue = true, IProjectEntry declaringScope = null);
    }
}
