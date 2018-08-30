using System;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class ParameterResultAssertions : ReferenceTypeAssertions<ParameterResult, ParameterResultAssertions> {
        public ParameterResultAssertions(ParameterResult subject) {
            Subject = subject;
        }

        protected override string Identifier => nameof(ParameterResult);

        public AndConstraint<ParameterResultAssertions> HaveName(string name, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.Name, name, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected '{Subject.Name}' parameter to have name '{name}'{{reason}}.");

            return new AndConstraint<ParameterResultAssertions>(this);
        }

        public AndConstraint<ParameterResultAssertions> HaveType(string type, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.Type, type, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected '{Subject.Name}' parameter to have type '{type}'{{reason}}, but it has type '{Subject.Type}'.");

            return new AndConstraint<ParameterResultAssertions>(this);
        }

        public AndConstraint<ParameterResultAssertions> HaveDefaultValue(string defaultValue, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(!string.IsNullOrEmpty(Subject.DefaultValue))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected '{Subject.Name}' parameter to have default value{defaultValue}{{reason}}, but it has '{Subject.DefaultValue}'.");

            return new AndConstraint<ParameterResultAssertions>(this);
        }

        public AndConstraint<ParameterResultAssertions> HaveNoDefaultValue(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.IsNullOrEmpty(Subject.DefaultValue))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected '{Subject.Name}' parameter to have no default value{{reason}}, but it has '{Subject.DefaultValue}'.");

            return new AndConstraint<ParameterResultAssertions>(this);
        }
    }
}