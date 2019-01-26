using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Python.Analysis.Indexing {
    internal class DirectoryFileReader {
        private readonly string _directoryPath;
        private readonly string[] _includeFiles;
        private readonly string[] _excludeFiles;

        public DirectoryFileReader(string directoryPath, string[] includeFiles, string[] excludeFiles) {
            _directoryPath = directoryPath;
            _includeFiles = includeFiles;
            _excludeFiles = excludeFiles;
        } 

        private Matcher BuildMatcher() {
            Matcher matcher = new Matcher();
            matcher.AddIncludePatterns(_includeFiles.IsNullOrEmpty() ? new[] { "**/*" } : _includeFiles);
            matcher.AddExcludePatterns(_excludeFiles ?? Enumerable.Empty<string>());
            return matcher;
        }

        public IEnumerable<string> DirectoryFilePaths() {
            var matcher = BuildMatcher();
            var dib = new DirectoryInfoWrapper(new DirectoryInfo(_directoryPath));
            var matchResult = matcher.Execute(dib);

            foreach (var file in matchResult.Files) {
                var path = Path.Combine(_directoryPath, PathUtils.NormalizePath(file.Path));
                yield return path;
            }
        }
    }
}
