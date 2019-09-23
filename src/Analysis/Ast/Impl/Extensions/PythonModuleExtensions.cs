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
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class PythonModuleExtensions {
        internal static PythonAst GetAst(this IPythonModule module)
            => (PythonAst)(module as IAstNodeContainer)?.GetAstNode(module);

        internal static void SetAst(this IPythonModule module, PythonAst ast) {
            var contained = (IAstNodeContainer)module;
            contained.ClearContent();
            contained.AddAstNode(module, ast);
        }

        internal static T GetAstNode<T>(this IPythonModule module, object o) where T : Node
            => (T)((IAstNodeContainer)module).GetAstNode(o);
        internal static void AddAstNode(this IPythonModule module, object o, Node n)
            => ((IAstNodeContainer)module).AddAstNode(o, n);

        /// <summary>
        /// Returns the string line corresponding to the given line number
        /// </summary>
        /// <param name="lineNum">The line number</param>
        internal static string GetLine(this IPythonModule module, int lineNum) {
            var content = module.Analysis?.Document?.Content;
            if (string.IsNullOrEmpty(content)) {
                return string.Empty;
            }

            var source = new SourceLocation(lineNum, 1);
            var start = module.GetAst().LocationToIndex(source);
            var end = start;


            for (; end < content.Length && content[end] != '\n' && content[end] != '\r'; end++) ;
            return content.Substring(start, end - start).Trim('\t', ' ');
        }

        /// <summary>
        /// Returns the comment corresponding to the given line or an empty string if there is no comment
        /// </summary>
        /// <param name="lineNum">The line number</param>
        internal static string GetComment(this IPythonModule module, int lineNum) {
            var line = module.GetLine(lineNum);

            var commentPos = line.IndexOf('#');
            if (commentPos < 0) {
                return string.Empty;
            }

            return line.Substring(commentPos + 1).Trim('\t', ' ');
        }

        public static bool IsNonUserFile(this IPythonModule module) => module.ModuleType.IsNonUserFile();
        public static bool IsCompiled(this IPythonModule module) => module.ModuleType.IsCompiled();
        public static bool IsTypingModule(this IPythonModule module) => module?.ModuleType == ModuleType.Specialized && module.Name == "typing";

        public static IEnumerable<string> GetSubmoduleNames(this IPythonModule module) {
            var searchResult = module.Interpreter.ModuleResolution.CurrentPathResolver.FindImports(module.FilePath, Enumerable.Empty<string>(), 1, true);
            if (searchResult is IImportChildrenSource children) {
                foreach (var name in children.GetChildrenNames()) {
                    if (children.TryGetChildImport(name, out var imports)) {
                        switch (imports) {
                            case ImplicitPackageImport packageImport:
                                yield return packageImport.Name;
                                break;
                            case ModuleImport moduleImport when !moduleImport.ModulePath.PathEquals(module.FilePath):
                                yield return moduleImport.Name;
                                break;
                        }
                    }
                }
            }
        }

        public static IPythonModule GetSubmodule(this IPythonModule module, string subModuleName) {
            var mres = module.Interpreter.ModuleResolution;
            var searchResult = mres.CurrentPathResolver.FindImports(module.FilePath, Enumerable.Repeat(subModuleName, 1), 1, true);
            if (searchResult is IImportChildrenSource children) {
                if (children.TryGetChildImport(subModuleName, out var imports)) {
                    switch (imports) {
                        case ImplicitPackageImport packageImport:
                            return mres.GetImportedModule(packageImport.FullName);
                        case ModuleImport moduleImport when !moduleImport.ModulePath.PathEquals(module.FilePath):
                            return mres.GetImportedModule(moduleImport.FullName);
                    }
                }
            }
            return null;
        }
    }
}
