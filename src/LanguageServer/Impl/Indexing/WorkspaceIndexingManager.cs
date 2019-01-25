using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Python.LanguageServer.Indexing {
    class WorkspaceIndexingManager {
        private string _rootDir;
        private string[] _includeFiles;
        private string[] _excludeFiles;
        private IndexingFileParser _indexingFileParser;
        private Matcher _matcher;

        public WorkspaceIndexingManager(string rootDir, string[] includeFiles, string[] excludeFiles, IndexingFileParser indexingFileParser) {
            _rootDir = rootDir;
            _includeFiles = includeFiles;
            _excludeFiles = excludeFiles;
            _indexingFileParser = indexingFileParser;
            if (string.IsNullOrEmpty(_rootDir)) {
                throw new ArgumentException($"{nameof(rootDir)} null or empty", nameof(rootDir));
            }
            InitializeMatcher();
        }

        private Task<IEnumerable<string>> AsyncDirectoryFilePaths() {
            return Task.Run(() => {
                return DirectoryFilePaths();
            });
        }

        private void InitializeMatcher() {
            _matcher = new Matcher();
            _matcher.AddIncludePatterns(_includeFiles.IsNullOrEmpty() ? new[] { "**/*" } : _includeFiles);
            _matcher.AddExcludePatterns(_excludeFiles ?? Enumerable.Empty<string>());
        }

        private IEnumerable<string> DirectoryFilePaths() {

            var dib = new DirectoryInfoWrapper(new DirectoryInfo(_rootDir));
            var matchResult = _matcher.Execute(dib);
            foreach (var file in matchResult.Files) {
                var path = Path.Combine(_rootDir, PathUtils.NormalizePath(file.Path));
                if (ModulePath.IsPythonSourceFile(path)) {
                    yield return path;
                }
            }
        }

        public async void ParseRootDirAsync() {
            foreach (var path in await AsyncDirectoryFilePaths()) {
                _indexingFileParser.ParseForIndex(path);
            }
        }

        public void StartParseRootDir() {
            Task.Run(() => {
                ParseRootDirAsync();
            }).Start();
        }

        public void ReParseIfOnWorkspace(Uri uri) {
            if (_matcher.Match(uri.AbsolutePath).HasMatches) {
                _indexingFileParser.ParseForIndex(uri.AbsolutePath);
            }
        }
    }
}
