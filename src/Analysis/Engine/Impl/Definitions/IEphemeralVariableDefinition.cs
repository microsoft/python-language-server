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

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// A variable def which was created on a read.  We need to create a variable def when
    /// we read from a class/instance where the member isn't defined yet - that lets us successfully
    /// get all of the references back if there is later an assignment.  But if there are
    /// no assignments then the variable doesn't really exist and we won't list it in the available members.
    /// </summary>
    public interface IEphemeralVariableDefinition {
        bool IsEphemeral { get; }
    }
}
