using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.LanguageServer;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class HoverAssertions : ReferenceTypeAssertions<Hover, HoverAssertions> {
        public HoverAssertions(Hover subject) {
            Subject = subject;
        }

        protected override string Identifier => nameof(Hover);

        public AndConstraint<HoverAssertions> HaveTypeName(string typeName, string because = "", params object[] reasonArgs)
            => HaveTypeNames(new[] {typeName}, because, reasonArgs);

        public AndConstraint<HoverAssertions> HaveTypeNames(string[] typeNames)
            => HaveTypeNames(typeNames, string.Empty);

        public AndConstraint<HoverAssertions> HaveTypeNames(IEnumerable<string> typeNames, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var expected = typeNames.ToArray();
            var actual = Subject._typeNames ?? Array.Empty<string>();
            var errorMessage = GetAssertCollectionContainsMessage(actual, expected, "hover", "type name", "type names");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<HoverAssertions>(this);
        }
    }
}