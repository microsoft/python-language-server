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
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Microsoft.Python.LanguageServer.CodeActions {
    public sealed class CodeActionSettings {
        public static readonly CodeActionSettings Default = new CodeActionSettings(refactoring: null, quickFix: null);

        private readonly IReadOnlyDictionary<string, object> _refactoring;
        private readonly IReadOnlyDictionary<string, object> _quickFix;

        public static CodeActionSettings Parse(JToken refactoring, JToken quickFix, CancellationToken cancellationToken) {
            var refactoringMap = new Dictionary<string, object>();
            var quickFixMap = new Dictionary<string, object>();

            AppendToMap(refactoring, GetPrefixLength(refactoring?.Path), refactoringMap, cancellationToken);
            AppendToMap(quickFix, GetPrefixLength(quickFix?.Path), quickFixMap, cancellationToken);

            return new CodeActionSettings(refactoringMap, quickFixMap);

            static int GetPrefixLength(string path) {
                // +1 is for last "." after prefix
                return string.IsNullOrEmpty(path) ? 0 : path.Length + 1;
            }
        }

        public CodeActionSettings(IReadOnlyDictionary<string, object> refactoring, IReadOnlyDictionary<string, object> quickFix) {
            _refactoring = refactoring ?? new Dictionary<string, object>();
            _quickFix = quickFix ?? new Dictionary<string, object>();
        }

        public T GetRefactoringOption<T>(string key, T defaultValue) => GetOption(_refactoring, key, defaultValue);
        public T GetQuickFixOption<T>(string key, T defaultValue) => GetOption(_quickFix, key, defaultValue);

        private T GetOption<T>(IReadOnlyDictionary<string, object> map, string key, T defaultValue) {
            try {
                if (map.TryGetValue(key, out var value)) {

                    // handle boxing case properly. 
                    // if value is boxing of "long", and asked for "int", asking for "int" on value
                    // will fail. 
                    // ex) value is T or (T)value will fail due to boxing.
                    // we first need to change boxed value to boxing of T and then cast.
                    // 
                    // ChangeType can throw if given typeof(T) is not supported
                    if (value is IConvertible) {
                        value = Convert.ChangeType(value, typeof(T));
                    }

                    return (T)value;
                }
            } catch {
                // ignore any option failure
            }

            return defaultValue;
        }

        private static void AppendToMap(JToken setting, int prefixLength, Dictionary<string, object> map, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            if (setting == null || !setting.HasValues) {
                return;
            }

            foreach (var child in setting) {
                cancellationToken.ThrowIfCancellationRequested();

                if (child is JValue value) {
                    // there shouldn't be duplicates and prefix must exist.
                    var path = child.Path;
                    if (path.Length <= prefixLength) {
                        // nothing to add
                        continue;
                    }

                    // get rid of common "settings.python..." prefix
                    map[path.Substring(prefixLength)] = value.Value;
                    continue;
                }

                if (child.Type == JTokenType.Array) {
                    // support options in the form of python.refactoring.xxx = [ .... ];
                    // in that case, key will be python.refactoring.xxx and value will be a map
                    var nestedMap = new Dictionary<string, object>();
                    AppendToNestedMap(child, name: null, nestedMap, cancellationToken);

                    map[child.Path.Substring(prefixLength)] = nestedMap;
                    continue;
                }

                AppendToMap(child, prefixLength, map, cancellationToken);
            }

            static void AppendToNestedMap(JToken token, string name, Dictionary<string, object> map, CancellationToken cancellationToken) {
                cancellationToken.ThrowIfCancellationRequested();

                if (token == null || !token.HasValues) {
                    return;
                }

                if (token is JProperty property) {
                    name = name == null ? property.Name : $"{name}.{property.Name}";
                }

                foreach (var child in token) {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (child is JValue value) {
                        // there shouldn't be duplicates and prefix must exist.
                        // get rid of common "settings.python..." prefix
                        map[name] = value.Value;
                        continue;
                    }

                    AppendToNestedMap(child, name, map, cancellationToken);
                }
            }
        }
    }
}
