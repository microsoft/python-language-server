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
using System.Linq;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;

namespace Microsoft.PythonTools.Analysis {
    internal class ModuleResolver {
        /// <summary>
        /// Returns a sequence of candidate absolute module names for the given
        /// modules.
        /// </summary>
        /// <param name="importingFromModuleName">
        /// The module that is importing the module.
        /// </param>
        /// <param name="importingFromFilePath">
        /// The path to the file that is importing the module.
        /// </param>
        /// <param name="relativeModuleName">
        /// A dotted name identifying the path to the module.
        /// </param>
        /// <returns>
        /// A sequence of strings representing the absolute names of the module
        /// in order of precedence.
        /// </returns>
        internal static IEnumerable<string> ResolvePotentialModuleNames(
            string importingFromModuleName,
            string importingFromFilePath,
            string relativeModuleName,
            bool absoluteImports
        ) {
            string importingFrom = string.Empty;
            if (!string.IsNullOrEmpty(importingFromModuleName)) {
                importingFrom = importingFromModuleName;
                if (!string.IsNullOrEmpty(importingFromFilePath) && ModulePath.IsInitPyFile(importingFromFilePath)) {
                    if (string.IsNullOrEmpty(importingFrom)) {
                        importingFrom = "__init__";
                    } else {
                        importingFrom += ".__init__";
                    }
                }
            }

            if (string.IsNullOrEmpty(relativeModuleName)) {
                yield break;
            }

            // Handle relative module names
            if (relativeModuleName.FirstOrDefault() == '.') {
                var fullName = GetModuleFullName(importingFrom, relativeModuleName);
                if (!fullName.StartsWithOrdinal(".") && !fullName.EndsWithOrdinal(".")) {
                    yield return fullName;
                }
                yield break;
            }

            // The two possible names that can be imported here are:
            // * relativeModuleName
            // * importingFrom.relativeModuleName
            // and the order they are returned depends on whether
            // absolute_import is enabled or not.

            // Assume trailing dots are not part of the import
            relativeModuleName = relativeModuleName.TrimEnd('.');

            // With absolute_import, we treat the name as complete first.
            if (absoluteImports) {
                yield return relativeModuleName;
            }

            if (!string.IsNullOrEmpty(importingFrom)) {
                var prefix = importingFrom.Split('.');

                if (prefix.Length > 1) {
                    var adjacentModuleName = string.Join(".", prefix.Take(prefix.Length - 1)) + "." + relativeModuleName;
                    yield return adjacentModuleName;
                }
            }

            // Without absolute_import, we treat the name as complete last.
            if (!absoluteImports) {
                yield return relativeModuleName;
            }
        }

        private static string GetModuleFullName(string originatingModule, string relativePath) {
            // Check if it is indeed relative
            if (string.IsNullOrEmpty(originatingModule) || string.IsNullOrEmpty(relativePath) || relativePath[0] != '.') {
                return relativePath;
            }

            var up = relativePath.TakeWhile(ch => ch == '.').Count();
            var bits = originatingModule.Split('.');
            if (up > bits.Length) {
                return relativePath; // too far up
            }

            var root = string.Join(".", bits.Take(bits.Length - up));
            var subPath = relativePath.Trim('.');

            return string.IsNullOrEmpty(root)
                ? subPath
                : string.IsNullOrEmpty(subPath) ? root : "{0}.{1}".FormatInvariant(root, subPath);
        }
    }
}
