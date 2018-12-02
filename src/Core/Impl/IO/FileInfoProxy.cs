﻿// Copyright(c) Microsoft Corporation
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

using System.IO;

namespace Microsoft.Python.Core.IO {
    public sealed class FileInfoProxy : IFileInfo {
        private readonly FileInfo _fileInfo;

        public FileInfoProxy(FileInfo fileInfo) {
            _fileInfo = fileInfo;
        }

        public bool Exists => _fileInfo.Exists;
        public string FullName => _fileInfo.FullName;
        public FileAttributes Attributes => _fileInfo.Attributes;
        public IDirectoryInfo Directory => _fileInfo.Directory != null ? new DirectoryInfoProxy(_fileInfo.Directory) : null;

        public StreamWriter CreateText() => _fileInfo.CreateText();

        public void Delete() => _fileInfo.Delete();
    }
}
