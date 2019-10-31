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

namespace Microsoft.Python.LanguageServer.CodeActions {
    public sealed class CodeActionSettings {
        public static readonly CodeActionSettings Default = new CodeActionSettings(refactoring: null, quickFix: null);

        private readonly IReadOnlyDictionary<string, object> _refactoring;
        private readonly IReadOnlyDictionary<string, object> _quickFix;

        public CodeActionSettings(IReadOnlyDictionary<string, object> refactoring, IReadOnlyDictionary<string, object> quickFix) {
            _refactoring = refactoring ?? new Dictionary<string, object>();
            _quickFix = quickFix ?? new Dictionary<string, object>();
        }

        public T GetRefactoringOption<T>(string key, T defaultValue) => GetOption(_refactoring, key, defaultValue);
        public T GetQuickFixOption<T>(string key, T defaultValue) => GetOption(_quickFix, key, defaultValue);

        private T GetOption<T>(IReadOnlyDictionary<string, object> map, string key, T defaultValue) {
            try {
                if (map.TryGetValue(key, out var value)) {
                    return (T)value;
                }

                return defaultValue;
            } catch {
                // ignore any option failure
            }

            return defaultValue;
        }
    }
}
