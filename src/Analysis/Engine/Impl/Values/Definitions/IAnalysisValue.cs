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

using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    public interface IAnalysisValue : IAnalysisSet, ICanExpire {
        /// <summary>
        /// Returns an immutable set which contains just this AnalysisValue.
        /// Currently implemented as returning the AnalysisValue object directly which implements ISet{AnalysisValue}.
        /// </summary>
        IAnalysisSet SelfSet { get; }

        /// <summary>
        /// Gets the name of the value if it has one, or null if it's a non-named item.
        /// The name property here is typically the same value you'd get by accessing __name__
        /// on the real Python object.
        /// </summary>
        string Name { get; }
        IAnalysisUnit AnalysisUnit { get; }

        IPythonType PythonType { get; }
        string Description { get; }
        string ShortDescription { get; }

        /// <summary>
        /// Gets the documentation of the value.
        /// </summary>
        string Documentation { get; }

        /// <summary>
        /// Gets a list of locations where this value is defined.
        /// </summary>
        IEnumerable<ILocationInfo> Locations { get; }


        /// <summary>
        /// Returns the member type of the analysis value, or PythonMemberType.Unknown if it's unknown.
        /// </summary>
        PythonMemberType MemberType { get; }
        BuiltinTypeId TypeId { get; }
        IPythonProjectEntry DeclaringModule { get; }
        int DeclaringVersion { get; }
        IAnalysisSet GetInstanceType();
        IEnumerable<OverloadResult> Overloads { get; }
        bool IsOfType(IAnalysisSet klass);

        /// <summary>
        /// Gets the constant value that this object represents, if it's a constant.
        /// Returns Type.Missing if the value is not constant (because it returns null
        /// if the type is None).
        /// </summary>
        object GetConstantValue();

        /// <summary>
        /// Returns the constant value as a string.  This returns a string if the constant
        /// value is either a unicode or ASCII string.
        /// </summary>
        string GetConstantValueAsString();

        IMro Mro { get; }

        /// <summary>
        /// Returns a list of key/value pairs stored in the this object which are retrivable using
        /// indexing.  For lists the key values will be integers (potentially constant, potentially not), 
        /// for dicts the key values will be arbitrary analysis values.
        /// </summary>
        IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems();

        /// <summary>
        /// Attempts to get a member from this object with the specified name.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="name">The name of the member.</param>
        /// <remarks>
        /// Overrides of this method must unconditionally call the base
        /// implementation, even if the return value is ignored.
        /// </remarks>
        IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name);

        IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None);
    }
}
