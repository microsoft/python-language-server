using System;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using FluentAssertions.Formatting;
using FluentAssertions.Primitives;

namespace Microsoft.Python.Parsing.Tests {
    internal static class ErrorResultExtensions {
        public static string Format(this ErrorResult err) {
            var s = err.Span.Start;
            var e = err.Span.End;
            return $"new ErrorResult(\"{err.Message}\", new SourceSpan({s.Line}, {s.Column}, {e.Line}, {e.Column}))";
        }
    }

    internal static class ErrorResultArrayExtensions {
        public static ErrorResultArrayAssertions Should(this ErrorResult[] instance) {
            return new ErrorResultArrayAssertions(instance);
        }
    }

    internal class ErrorResultArrayAssertions : CollectionAssertions<ErrorResult[], ErrorResultArrayAssertions> {
        public ErrorResultArrayAssertions(ErrorResult[] instance) {
            Subject = instance;
        }

        protected override string Identifier => "error result array";

        public AndConstraint<ErrorResultArrayAssertions> HaveErrors(ErrorResult[] expected) {
            for (var i = 0; i < expected.Length; i++) {
                Execute.Assertion
                    .ForCondition(Subject.Length > i)
                    .FailWith("No error {0}: {1}", i, expected[i].Format());

                Execute.Assertion
                    .ForCondition(Subject[i].Message == expected[i].Message)
                    .FailWith("Wrong msg for error {0}: expected {1}, got {2}", i, expected[i].Format(), Subject[i].Format());

                Execute.Assertion
                    .ForCondition(Subject[i].Span == expected[i].Span)
                    .FailWith("Wrong span for error {0}: expected {1}, got {2}", i, expected[i].Format(), Subject[i].Format());
            }

            Execute.Assertion
                .ForCondition(expected.Length <= Subject.Length)
                .FailWith("Unexpected errors occurred");

            return new AndConstraint<ErrorResultArrayAssertions>(this);
        }
    }
}
