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

using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public static class PathResolverExtensions {
        /// <summary>
        /// Given module file path and module name expression locates the module.
        /// Module name expression can be absolute or relative. <see cref="ModuleName"/>
        /// and <see cref="RelativeModuleName"/>.
        /// </summary>
        /// <returns></returns>
        public static IImportSearchResult FindImports(this PathResolverSnapshot pathResolver, string modulePath, ModuleName moduleImportExpression, bool forceAbsolute) {
            var rootNames = moduleImportExpression.Names.Select(n => n.Name);
            return moduleImportExpression is RelativeModuleName relativeName
                ? pathResolver.GetImportsFromRelativePath(modulePath, relativeName.DotCount, rootNames)
                : pathResolver.GetImportsFromAbsoluteName(modulePath, rootNames, forceAbsolute);
        }
    }
}
