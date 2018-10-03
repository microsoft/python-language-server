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
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    internal sealed class VariableDefTestInfo {
        private readonly IVariableDefinition _variableDef;
        private readonly IScope _scope;
        public string Name { get; }

        public VariableDefTestInfo(IVariableDefinition variableDef, string name, IScope scope) {
            _variableDef = variableDef;
            Name = name;
            _scope = scope;
        }

        public VariableDefAssertions Should() => new VariableDefAssertions(_variableDef, Name, _scope);
    }

    internal sealed class VariableDefAssertions : ReferenceTypeAssertions<IVariableDefinition, VariableDefAssertions> {
        private readonly string _moduleName;
        private readonly string _name;
        private readonly IScope _scope;

        public VariableDefAssertions(IVariableDefinition variableDef, string name, IScope scope) {
            Subject = variableDef;
            _name = name;
            _scope = scope;
            _moduleName = scope.Name;
        }

        protected override string Identifier => nameof(IVariableDefinition);

        public AndConstraint<VariableDefAssertions> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs)
            => HaveTypes(new[]{ typeId }, because, reasonArgs);

        public AndConstraint<VariableDefAssertions> HaveTypes(params BuiltinTypeId[] typeIds)
            => HaveTypes(typeIds, string.Empty);

        public AndConstraint<VariableDefAssertions> HaveTypes(IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            var languageVersionIs3X = Is3X(_scope);
            AssertTypeIds(Subject.Types, typeIds, $"{_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs);

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveNoTypes(string because = "", params object[] reasonArgs) {
            var languageVersionIs3X = Is3X(_scope);
            AssertTypeIds(Subject.Types, new BuiltinTypeId[0], $"{_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs);

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveResolvedType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs)
            => HaveResolvedTypes(new[]{ typeId }, because, reasonArgs);

        public AndConstraint<VariableDefAssertions> HaveResolvedTypes(params BuiltinTypeId[] typeIds)
            => HaveResolvedTypes(typeIds, string.Empty);

        public AndConstraint<VariableDefAssertions> HaveResolvedTypes(IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            var resolved = Subject.Types.Resolve(new AnalysisUnit(null, null, _scope, true));

            var languageVersionIs3X = Is3X(_scope);
            AssertTypeIds(resolved, typeIds, $"{_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs);

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveMergedType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs)
            => HaveMergedTypes(new[]{ typeId }, because, reasonArgs);

        public AndConstraint<VariableDefAssertions> HaveMergedTypes(params BuiltinTypeId[] typeIds)
            => HaveMergedTypes(typeIds, string.Empty);

        public AndConstraint<VariableDefAssertions> HaveMergedTypes(IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = _scope.GetMergedVariableTypes(_name);
            var languageVersionIs3X = Is3X(_scope);
            AssertTypeIds(actual, typeIds, $"variable {_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs, "merged type", "merged types");

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveResolvedClassName(string className, string because = "", params object[] reasonArgs)
            => HaveResolvedClassNames(new[] { className }, because, reasonArgs);

        public AndConstraint<VariableDefAssertions> HaveResolvedClassNames(params string[] classNames)
            => HaveResolvedClassNames(classNames, string.Empty);

        public AndConstraint<VariableDefAssertions> HaveResolvedClassNames(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            var resolved = Subject.Types.Resolve(new AnalysisUnit(null, null, _scope, true));
            var expected = classNames.ToArray();
            var actual = FlattenAnalysisValues(resolved).Select(av => av.ShortDescription).ToArray();

            var message = GetAssertCollectionOnlyContainsMessage(actual, expected, $"variable '{_moduleName}.{_name}'", "resolved type", "resolved types");
            Execute.Assertion.ForCondition(message == null)
                    .BecauseOf(because, reasonArgs)
                    .FailWith(message);

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveClassName(string className, string because = "", params object[] reasonArgs)
            => HaveClassNames(new[] { className }, because, reasonArgs);

        public AndConstraint<VariableDefAssertions> HaveClassNames(params string[] classNames)
            => HaveClassNames(classNames, string.Empty);

        public AndConstraint<VariableDefAssertions> HaveClassNames(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            var values = FlattenAnalysisValues(Subject.Types).ToArray();

            var actualMemberTypes = values.Select(av => av.MemberType).ToArray();
            var expectedMemberTypes = new []{ PythonMemberType.Instance };
            var actualDescription = FlattenAnalysisValues(Subject.Types).Select(av => av.ShortDescription).ToArray();
            var expectedDescription = classNames.ToArray();

            var message = GetAssertCollectionContainsMessage(actualMemberTypes, expectedMemberTypes, $"variable '{_moduleName}.{_name}'", "member type", "member types")
                ?? GetAssertCollectionOnlyContainsMessage(actualDescription, expectedDescription, $"variable '{_moduleName}.{_name}'", "type", "types");

            Execute.Assertion.ForCondition(message == null)
                    .BecauseOf(because, reasonArgs)
                    .FailWith(message);

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveMemberType(PythonMemberType memberType, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Types is IAnalysisValue av && av.MemberType == memberType)
                .BecauseOf(because, reasonArgs)
                .FailWith(Subject.Types != null
                    ? $"Expected {_moduleName}.{_name} to be {memberType}, but it is {((AnalysisValue) Subject.Types).MemberType} {{reason}}."
                    : $"Expected {_moduleName}.{_name} to be {memberType} {{reason}}.");

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveDescription(string description, string because = "", params object[] reasonArgs) {
            var values = FlattenAnalysisValues(Subject.Types).ToArray();
            var value = AssertSingle(because, reasonArgs, values);

            var actualDescription = value.Description;
            var actualShortDescription = value.ShortDescription;
            Execute.Assertion.ForCondition(description == actualDescription || description == actualShortDescription)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected description of '{_moduleName}.{_name}' to have description '{description}'{{reason}}, but found '{actualDescription}' or '{actualShortDescription}'.");

            return new AndConstraint<VariableDefAssertions>(this);
        }

        public AndConstraint<VariableDefAssertions> HaveDocumentation(string documentation, string because = "", params object[] reasonArgs) {
            var values = FlattenAnalysisValues(Subject.Types).ToArray();
            var value = AssertSingle(because, reasonArgs, values);

            var actualDocumentation = value.Documentation;
            Execute.Assertion.ForCondition(string.Equals(documentation, actualDocumentation, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected documentation of '{_moduleName}.{_name}' to have documentation '{documentation}'{{reason}}, but found '{actualDocumentation}'.");

            return new AndConstraint<VariableDefAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<VariableDefAssertions> HaveShortDescriptions(params string[] descriptions) => HaveShortDescriptions(descriptions, string.Empty);

        [CustomAssertion]
        public AndConstraint<VariableDefAssertions> HaveShortDescriptions(IEnumerable<string> descriptions, string because = "", params object[] reasonArgs) {
            var actual = FlattenAnalysisValues(Subject.Types).Select(t => t.ShortDescription).ToArray();
            var expected = descriptions.ToArray();
            var errorMessage = GetAssertCollectionOnlyContainsMessage(actual, expected, $"{_moduleName}.{_name}", "description", "descriptions");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<VariableDefAssertions>(this);
        }
        
        public AndWhichConstraint<VariableDefAssertions, AnalysisValueTestInfo<TValue>> HaveValue<TValue>(string because = "", params object[] reasonArgs)
            where TValue : IAnalysisValue {
            var values = FlattenAnalysisValues(Subject.Types).ToArray();
            var value = AssertSingle(because, reasonArgs, values);

            Execute.Assertion.ForCondition(value is TValue)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {_moduleName}.{_name} to have value of type {typeof(TValue)}{{reason}}, but its value has type {value.GetType()}.");

            var testInfo = new AnalysisValueTestInfo<TValue>((TValue)value, GetScopeDescription(), _scope);
            return new AndWhichConstraint<VariableDefAssertions, AnalysisValueTestInfo<TValue>>(this, testInfo);
        }
        
        private IAnalysisValue AssertSingle(string because, object[] reasonArgs, IAnalysisValue[] values) {
            Execute.Assertion.ForCondition(values.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(values.Length == 0 
                    ? $"Expected variable '{_moduleName}.{_name}' to have single value{{reason}}, but found none."
                    : $"Expected variable '{_moduleName}.{_name}' to have single value{{reason}}, but found {values.Length}: {GetQuotedNames(values)}");

            return values[0];
        }

        private string GetScopeDescription() {
            switch (_scope) {
                case IFunctionScope functionScope:
                    return $"of variable '{_name}' in function {GetQuotedName(functionScope.Function)}";
                default:
                    return $"of variable '{_moduleName}.{_name}'";
            }
        }
    }
}