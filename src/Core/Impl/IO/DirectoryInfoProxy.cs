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
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Microsoft.Python.Core.IO {
    public sealed class DirectoryInfoProxy : IDirectoryInfo {
        private readonly DirectoryInfo _directoryInfo;

        public DirectoryInfoProxy(string directoryPath) {
            _directoryInfo = new DirectoryInfo(directoryPath);
        }

        public DirectoryInfoProxy(DirectoryInfo directoryInfo) {
            _directoryInfo = directoryInfo;
        }

        public bool Exists => _directoryInfo.Exists;
        public string FullName => _directoryInfo.FullName;
        public FileAttributes Attributes => _directoryInfo.Attributes;
        public IDirectoryInfo Parent => _directoryInfo.Parent != null ? new DirectoryInfoProxy(_directoryInfo.Parent) : null;

        public void Delete() => _directoryInfo.Delete();

        public IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos() => _directoryInfo
                .EnumerateFileSystemInfos()
                .Select(CreateFileSystemInfoProxy);

        public IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string[] includeFiles, string[] excludeFiles) {
            Matcher matcher = new Matcher();
            matcher.AddIncludePatterns(includeFiles.IsNullOrEmpty() ? new[] { "**/*" } : includeFiles);
            matcher.AddExcludePatterns(excludeFiles ?? Enumerable.Empty<string>());
            var matchResult = matcher.Execute(new DirectoryInfoWrapper(_directoryInfo));
            return matchResult.Files.Select((filePatternMatch) => {
                var fileSystemInfo = _directoryInfo.GetFileSystemInfos(filePatternMatch.Stem).First();
                return CreateFileSystemInfoProxy(fileSystemInfo);
            });
        }

        private static IFileSystemInfo CreateFileSystemInfoProxy(FileSystemInfo fileSystemInfo)
            => fileSystemInfo is DirectoryInfo directoryInfo
                ? (IFileSystemInfo)new DirectoryInfoProxy(directoryInfo)
                : new FileInfoProxy((FileInfo)fileSystemInfo);
    }
}
