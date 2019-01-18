using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Indexing;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.Python.LanguageServer.Implementation {
    class IndexPopulator {
        private SymbolIndex _symbolIndex;
        private string _rootDir;
        private string[] _incluideFiles;
        private string[] _excludeFiles;
        private PythonLanguageVersion _languageVersion;

        public IndexPopulator(SymbolIndex symbolIndex, string rootDir, string[] includeFiles, string[] excludeFiles, PythonLanguageVersion languageVersion) {
            _symbolIndex = symbolIndex;
            _rootDir = rootDir;
            _incluideFiles = includeFiles;
            _excludeFiles = excludeFiles;
            _languageVersion = languageVersion;
            if (string.IsNullOrEmpty(_rootDir)) {
                throw new ArgumentException($"{nameof(rootDir)} null or empty", nameof(rootDir));
            }
        }

        private IEnumerable<string> DirectoryFilePaths() {
            var matcher = new Matcher();
            matcher.AddIncludePatterns(_incluideFiles.IsNullOrEmpty() ? new[] { "**/*" } : _incluideFiles);
            matcher.AddExcludePatterns(_excludeFiles ?? Enumerable.Empty<string>());

            var dib = new DirectoryInfoWrapper(new DirectoryInfo(_rootDir));
            var matchResult = matcher.Execute(dib);

            foreach (var file in matchResult.Files) {
                var path = Path.Combine(_rootDir, PathUtils.NormalizePath(file.Path));
                if (ModulePath.IsPythonSourceFile(path)) {
                    yield return path;
                }
            }
        }

        public void Populate() {
            foreach (var path in DirectoryFilePaths()) {
                using (var stream = new StreamReader(path)) {
                    var parser = Parser.CreateParser(stream, _languageVersion);
                    var pythonAst = parser.ParseFile();
                    _symbolIndex.UpdateParseTree(new Uri(path), pythonAst);
                }
            }
        }
        
    }
}
