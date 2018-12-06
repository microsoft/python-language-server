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
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis {
    /// <summary>
    /// Specifies creation options for an interpreter factory.
    /// </summary>
    public sealed class InterpreterFactoryCreationOptions {
        public InterpreterFactoryCreationOptions Clone() => (InterpreterFactoryCreationOptions)MemberwiseClone();

        public string DatabasePath { get; set; }

        public bool UseExistingCache { get; set; } = true;

        #region Dictionary serialization

        public static InterpreterFactoryCreationOptions FromDictionary(Dictionary<string, object> properties) {
            var opts = new InterpreterFactoryCreationOptions {
                DatabasePath = properties.TryGetValue("DatabasePath", out var o) ? (o as string) : null,
                UseExistingCache = ReadBool(properties, nameof(UseExistingCache)) ?? true,
            };

            return opts;
        }

        public Dictionary<string, object> ToDictionary(bool suppressFileWatching = true) {
            var d = new Dictionary<string, object> {
                [nameof(DatabasePath)] = DatabasePath,
                [nameof(UseExistingCache)] = UseExistingCache
            };
            return d;
        }

        private static bool? ReadBool(IReadOnlyDictionary<string, object> properties, string key) {
            if (properties.TryGetValue(key, out var o)) {
                return (o as bool?) ?? (o as string)?.IsTrue();
            }
            return null;
        }
        #endregion
    }
}
