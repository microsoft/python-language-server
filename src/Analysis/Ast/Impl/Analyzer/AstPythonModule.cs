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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public class AstPythonModule : PythonModuleType, IPythonModule, ILocatedMember {
        private readonly IPythonInterpreter _interpreter;
        private readonly List<string> _childModules = new List<string>();
        private readonly ConcurrentDictionary<string, IMember> _members = new ConcurrentDictionary<string, IMember>();
        private bool _foundChildModules;
        private string _documentation = string.Empty;

        internal AstPythonModule() : base(string.Empty) {
            _foundChildModules = true;
        }

        protected AstPythonModule(string moduleName, IPythonInterpreter interpreter, string filePath, Uri uri) :
            base(moduleName, filePath, uri) {
            Check.ArgumentNotNull(nameof(filePath), filePath);
            Check.ArgumentNotNull(nameof(interpreter), interpreter);
            Check.ArgumentNotNull(nameof(uri), uri);

            Locations = new[] { new LocationInfo(filePath, uri, 1, 1) };
            _interpreter = interpreter;

            // Do not allow children of named modules
            _foundChildModules = !ModulePath.IsInitPyFile(FilePath);
        }

        protected virtual PythonAst GetAst() => null;

        public override string Documentation {
            get {
                _documentation = _documentation ?? GetAst()?.Documentation;
                if (_documentation == null) {
                    _members.TryGetValue("__doc__", out var m);
                    _documentation = (m as AstPythonStringLiteral)?.Value ?? string.Empty;
                    if (string.IsNullOrEmpty(_documentation)) {
                        _members.TryGetValue($"_{Name}", out m);
                        _documentation = (m as AstNestedPythonModule)?.Documentation;
                        if (string.IsNullOrEmpty(_documentation)) {
                            _documentation = TryGetDocFromModuleInitFile(FilePath);
                        }
                    }
                }
                return _documentation;
            }
        }
        public IEnumerable<LocationInfo> Locations { get; } = Enumerable.Empty<LocationInfo>();

        private static IEnumerable<string> GetChildModuleNames(string filePath, string prefix, IPythonInterpreter interpreter) {
            if (interpreter == null || string.IsNullOrEmpty(filePath)) {
                yield break;
            }
            var searchPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(searchPath)) {
                yield break;
            }

            foreach (var n in ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n))) {
                yield return n;
            }
        }

        public IEnumerable<string> GetChildrenModuleNames() {
            lock (_childModules) {
                if (!_foundChildModules) {
                    // We've already checked whether this module may have children
                    // so don't worry about checking again here.
                    _foundChildModules = true;
                    foreach (var childModuleName in GetChildModuleNames(FilePath, Name, _interpreter)) {
                        _members[childModuleName] = new AstNestedPythonModule(_interpreter, Name + "." + childModuleName);
                        _childModules.Add(childModuleName);
                    }
                }
                return _childModules.ToArray();
            }
        }

        public override IMember GetMember(string name) {
            IMember member = null;
            _members.TryGetValue(name, out member);
            if (member is ILazyMember lm) {
                member = lm.Get();
                lock (_members) {
                    _members[name] = member;
                }
            }
            return member;
        }

        public override IEnumerable<string> GetMemberNames() {
            lock (_members) {
                return _members.Keys.ToArray();
            }
        }

        public void NotifyImported() { }
        public void RemovedFromProject() { }

        private static string TryGetDocFromModuleInitFile(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return string.Empty;
            }

            try {
                using (var sr = new StreamReader(filePath)) {
                    string quote = null;
                    string line;
                    while (true) {
                        line = sr.ReadLine()?.Trim();
                        if (line == null) {
                            break;
                        }
                        if (line.Length == 0 || line.StartsWithOrdinal("#")) {
                            continue;
                        }
                        if (line.StartsWithOrdinal("\"\"\"") || line.StartsWithOrdinal("r\"\"\"")) {
                            quote = "\"\"\"";
                        } else if (line.StartsWithOrdinal("'''") || line.StartsWithOrdinal("r'''")) {
                            quote = "'''";
                        }
                        break;
                    }

                    if (quote != null) {
                        // Check if it is a single-liner
                        if (line.EndsWithOrdinal(quote) && line.IndexOf(quote) < line.LastIndexOf(quote)) {
                            return line.Substring(quote.Length, line.Length - 2 * quote.Length).Trim();
                        }
                        var sb = new StringBuilder();
                        while (true) {
                            line = sr.ReadLine();
                            if (line == null || line.EndsWithOrdinal(quote)) {
                                break;
                            }
                            sb.AppendLine(line);
                        }
                        return sb.ToString();
                    }
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return string.Empty;
        }
    }
}
