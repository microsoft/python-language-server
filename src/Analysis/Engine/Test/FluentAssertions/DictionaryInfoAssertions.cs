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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class DictionaryInfoAssertions : AnalysisValueAssertions<DictionaryInfo, DictionaryInfoAssertions> {
        public DictionaryInfoAssertions(AnalysisValueTestInfo<DictionaryInfo> subject) : base(subject) {}

        protected override string Identifier => nameof(DictionaryInfo);

        public AndConstraint<DictionaryInfoAssertions> HaveValueType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) 
            => HaveValueTypes(new []{ typeId }, because, reasonArgs);

        public AndConstraint<DictionaryInfoAssertions> HaveValueTypes(params BuiltinTypeId[] typeIds) 
            => HaveValueTypes(typeIds, string.Empty);

        public AndConstraint<DictionaryInfoAssertions> HaveValueTypes(IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            var is3X = ((ModuleScope)OwnerScope.GlobalScope).Module.ProjectEntry.ProjectState.LanguageVersion.Is3x();
            AssertionsUtilities.AssertTypeIds(Subject._keysAndValues.AllValueTypes, typeIds, GetName(), is3X, because, reasonArgs);
            return new AndConstraint<DictionaryInfoAssertions>(this);
        }
    }
}