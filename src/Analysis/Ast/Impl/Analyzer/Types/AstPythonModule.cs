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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Analyzer.Modules;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    public class AstPythonModule : PythonModuleType, ILocatedMember {
        private readonly IFileSystem _fs;
        private string _documentation = string.Empty;

        internal AstPythonModule() : base(string.Empty) { }

        protected AstPythonModule(string moduleName, string filePath, Uri uri, IPythonInterpreter interpreter) :
            base(moduleName, filePath, uri, interpreter) {
            _fs = interpreter.Services.GetService<IFileSystem>();
            Locations = new[] { new LocationInfo(filePath, uri, 1, 1) };
        }

        public override string Documentation {
            get {
                _documentation = _documentation ?? Ast?.Documentation;
                if (_documentation == null) {
                    var m = GetMember("__doc__");
                    _documentation = (m as AstPythonStringLiteral)?.Value ?? string.Empty;
                    if (string.IsNullOrEmpty(_documentation)) {
                        m = GetMember($"_{Name}");
                        _documentation = (m as LazyPythonModule)?.Documentation;
                        if (string.IsNullOrEmpty(_documentation)) {
                            _documentation = TryGetDocFromModuleInitFile();
                        }
                    }
                }

                return _documentation;
            }
        }

        public IEnumerable<LocationInfo> Locations { get; } = Enumerable.Empty<LocationInfo>();

        private IEnumerable<string> GetChildModuleNames(string filePath, string prefix, IPythonInterpreter interpreter) {
            if (interpreter == null || string.IsNullOrEmpty(filePath)) {
                yield break;
            }
            var searchPath = Path.GetDirectoryName(filePath);
            if (!_fs.DirectoryExists(searchPath)) {
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

        public override IEnumerable<string> GetChildrenModuleNames()
            => GetChildModuleNames(FilePath, Name, Interpreter);

        internal override string GetCode() => _fs.ReadAllText(FilePath);

        private string TryGetDocFromModuleInitFile() {
            if (string.IsNullOrEmpty(FilePath) || !_fs.FileExists(FilePath)) {
                return string.Empty;
            }

            try {
                using (var sr = new StreamReader(FilePath)) {
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
