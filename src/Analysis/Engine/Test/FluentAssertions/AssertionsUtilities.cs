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
using System.Text;
using FluentAssertions.Execution;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    internal static class AssertionsUtilities {
        public static bool Is3X(IScope scope) 
            => ((ModuleScope)scope.GlobalScope).Module.ProjectEntry.ProjectState.LanguageVersion.Is3x();

        public static void AssertTypeIds(IEnumerable<IAnalysisValue> actualTypeIds, IEnumerable<BuiltinTypeId> typeIds, string name, bool languageVersionIs3X, string because, object[] reasonArgs)
            => AssertTypeIds(FlattenAnalysisValues(actualTypeIds).Select(av => av.PythonType?.TypeId ?? av.TypeId), typeIds, name, languageVersionIs3X, because, reasonArgs);

        public static void AssertTypeIds(IEnumerable<BuiltinTypeId> actualTypeIds, IEnumerable<BuiltinTypeId> typeIds, string name, bool languageVersionIs3X, string because, object[] reasonArgs) {
            var expected = typeIds.Select(t => {
                switch (t) {
                    case BuiltinTypeId.Str:
                        return languageVersionIs3X ? BuiltinTypeId.Unicode : BuiltinTypeId.Bytes;
                    case BuiltinTypeId.StrIterator:
                        return languageVersionIs3X ? BuiltinTypeId.UnicodeIterator : BuiltinTypeId.BytesIterator;
                    default:
                        return t;
                }
            }).ToArray();

            var actual = actualTypeIds.ToArray();
            var errorMessage = GetAssertCollectionOnlyContainsMessage(actual, expected, name, "type ", "types ");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);
        }

        public static string GetAssertCollectionContainsMessage<T>(T[] actual, T[] expected, string name, string itemNameSingle, string itemNamePlural, Func<T[], string> itemsFormatter = null) {
            itemsFormatter = itemsFormatter ?? AssertCollectionDefaultItemsFormatter;
            var missing = expected.Except(actual).ToArray();

            if (missing.Length > 0) {
                return expected.Length > 1
                    ? $"Expected {name} to have {itemNamePlural}{itemsFormatter(expected)}{{reason}}, but it has {itemsFormatter(actual)}, which doesn't include {itemsFormatter(missing)}."
                    : expected.Length > 0 
                        ? $"Expected {name} to have {itemNameSingle}'{expected[0]}'{{reason}}, but it has {itemsFormatter(actual)}."
                        : $"Expected {name} to have no {itemNamePlural}{{reason}}, but it has {itemsFormatter(actual)}.";
            }

            return null;
        }

        public static string GetAssertCollectionNotContainMessage<T>(T[] actual, T[] expectedMissing, string name, string itemNameSingle, string itemNamePlural, Func<T[], string> itemsFormatter = null) {
            itemsFormatter = itemsFormatter ?? AssertCollectionDefaultItemsFormatter;
            var intersect = expectedMissing.Intersect(actual).ToArray();

            if (intersect.Length > 0) {
                return expectedMissing.Length > 1
                    ? $"Expected {name} to not contain {itemNamePlural}{itemsFormatter(expectedMissing)}{{reason}}, but it contains {itemsFormatter(intersect)}."
                    : $"Expected {name} to not contain {itemNameSingle}'{expectedMissing[0]}'{{reason}}.";
            }

            return null;
        }

        public static string GetAssertCollectionOnlyContainsMessage<T>(T[] actual, T[] expected, string name, string itemNameSingle, string itemNamePlural, Func<T[], string> itemsFormatter = null) {
            itemsFormatter = itemsFormatter ?? AssertCollectionDefaultItemsFormatter;
            var message = GetAssertCollectionContainsMessage(actual, expected, name, itemNameSingle, itemNamePlural, itemsFormatter);

            if (message != null) {
                return message;
            }

            var excess = expected.Length > 0 ? actual.Except(expected).ToArray() : actual;
            if (excess.Length > 0) {
                return expected.Length > 1
                    ? $"Expected {name} to have only {itemNamePlural}{itemsFormatter(expected)}{{reason}}, but it also has {itemsFormatter(excess)}."
                    : expected.Length > 0
                        ? $"Expected {name} to have only {itemNameSingle}'{expected[0]}'{{reason}}, but it also has {itemsFormatter(excess)}."
                        : $"Expected {name} to have no {itemNamePlural}{{reason}}, but it has {itemsFormatter(excess)}.";
            }

            return null;
        }
        
        public static string GetAssertSequenceEqualMessage<T>(T[] actual, T[] expected, string name, string itemNamePlural, Func<T[], string> itemsFormatter = null) {
            itemsFormatter = itemsFormatter ?? AssertCollectionDefaultItemsFormatter;

            for (var i = 0; i < actual.Length && i < expected.Length; i++) {
                if (!Equals(actual[i], expected[i])) {
                    return $"Expected {name} to have {itemNamePlural}{itemsFormatter(expected)}{{reason}}, but it has {itemsFormatter(actual)}, which is different at {i}: '{actual[i]}' instead of '{expected[i]}'.";
                }
            }

            if (actual.Length > expected.Length) {
                return $"Expected {name} to have {itemNamePlural}{itemsFormatter(expected)}{{reason}}, but it also has {itemsFormatter(actual.Skip(expected.Length).ToArray())} at the end.";
            }

            if (expected.Length > actual.Length) {
                return $"Expected {name} to have {itemNamePlural}{itemsFormatter(expected)}{{reason}}, but it misses {itemsFormatter(expected.Skip(actual.Length).ToArray())} at the end.";
            }

            return null;
        }
        
        public static string AssertCollectionDefaultItemsFormatter<T>(T[] items) 
            => items.Length > 1 
                ? "[{0}]".FormatInvariant(string.Join(", ", items)) 
                : items.Length == 1 ? $"'{items[0]}'" : "none";

        public static string GetQuotedNames(IEnumerable<object> values) {
            return GetQuotedNames(values.Select(GetName));
        }

        public static string GetQuotedNames(IEnumerable<string> names) {
            var sb = new StringBuilder();
            string previousName = null;
            foreach (var name in names) {
                sb.AppendQuotedName(previousName, ", ");
                previousName = name;
            }

            sb.AppendQuotedName(previousName, " and ");
            return sb.ToString();
        }

        private static StringBuilder AppendQuotedName(this StringBuilder stringBuilder, string name, string prefix) {
            if (!string.IsNullOrEmpty(name)) {
                if (stringBuilder.Length > 0) {
                    stringBuilder.Append(prefix);
                }

                stringBuilder
                    .Append("'")
                    .Append(name)
                    .Append("'");
            }

            return stringBuilder;
        }

        public static string GetQuotedName(object value) {
            string name;
            switch (value) {
                case IHasQualifiedName _:
                case IPythonModule _:
                case IBuiltinInstanceInfo _:
                    name = GetName(value);
                    return string.IsNullOrEmpty(name) ? string.Empty : $"'{name}'";
                case IAnalysisValue av:
                    name = av.Name;
                    return string.IsNullOrEmpty(name) ? "value" : $"value '{name}'";
                default:
                    name = GetName(value);
                    return string.IsNullOrEmpty(name) ? string.Empty : $"'{name}'";
            }
        }

        public static string GetName(object value) {
            switch (value) {
                case IHasQualifiedName qualifiedName:
                    return qualifiedName.FullyQualifiedName;
                case IPythonModule pythonModule:
                    return pythonModule.Name;
                case IBuiltinInstanceInfo builtinInstanceInfo:
                    return builtinInstanceInfo.Name ?? $"instance of {builtinInstanceInfo.ClassInfo.FullyQualifiedName}";
                case IScope scope:
                    return scope.Name;
                case IAnalysisValue analysisValue:
                    return $"value {analysisValue.Name}";
                case string str:
                    return str;
                default:
                    return string.Empty;
            }
        }
       
        public static IEnumerable<IAnalysisValue> FlattenAnalysisValues(IEnumerable<IAnalysisValue> analysisValues) {
            foreach (var analysisValue in analysisValues) {
                if (analysisValue is MultipleMemberInfo mmi) {
                    foreach (var value in FlattenAnalysisValues(mmi.Members)) {
                        yield return value;
                    }
                } else {
                    yield return analysisValue;
                }
            }
        }
    }
}