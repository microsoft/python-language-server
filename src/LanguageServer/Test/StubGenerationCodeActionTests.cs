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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.LanguageServer.CodeActions;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.LanguageServer.Tests.FluentAssertions;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.Python.UnitTests.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class StubGenerationCodeActionTests : LanguageServerTestBase {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task Missing() {
            MarkupUtils.GetSpan(@"import [|StubGenerationTest|]", out var code, out var span);

            var services = GetServiceManager();
            var analysis = await GetAnalysisAsync(code, sm: services);
            var stubGenerationSetting = new CodeActionSettings(new Dictionary<string, object>() {
                { "generation.stub", true }, { "generation.stub.path", Path.Combine(Path.GetTempPath(), "stubGenerationTest") } }, quickFix: null);

            var codeActions = await new RefactoringCodeActionSource(analysis.ExpressionEvaluator.Services).GetCodeActionsAsync(
                analysis, stubGenerationSetting, span.ToSourceSpan(analysis.Ast), CancellationToken.None);
            codeActions.Should().HaveCount(1);

            var codeAction = codeActions[0];
            await new CommandHandlerSource(analysis.ExpressionEvaluator.Services).HandleAsync(codeAction.command.command, codeAction.command.arguments, CancellationToken.None);

            var fileSystem = services.GetService<IFileSystem>() as MockFileSystem;
        }

        private ServiceManager GetServiceManager() {
            var services = new ServiceManager();

            var platform = new OSPlatform();
            services
                .AddService(TestLogger)
                .AddService(platform)
                .AddService(new ProcessServices())
                .AddService(new MockFileSystem(new FileSystem(platform)));

            return services;
        }

        private class MockFileSystem : IFileSystem {
            private readonly IFileSystem _delegatee;

            public readonly Dictionary<string, string> Contents = new Dictionary<string, string>();

            public MockFileSystem(IFileSystem delegatee) => _delegatee = delegatee;

            public void FileWriteAllBytes(string path, byte[] bytes) => Contents[path] = Encoding.UTF8.GetString(bytes);
            public void FileWriteAllLines(string path, IEnumerable<string> contents) => Contents[path] = string.Join(Environment.NewLine, contents);
            public void WriteAllText(string path, string content) => Contents[path] = content;

            public void CreateDirectory(string path) { }
            public Stream CreateFile(string path) => new MemoryStream();
            public void DeleteDirectory(string path, bool recursive) { }
            public void DeleteFile(string path) { }
            public void SetFileAttributes(string fullPath, FileAttributes attributes) { }

            public Stream FileOpen(string path, FileMode mode) => _delegatee.FileOpen(path, mode);
            public Stream FileOpen(string path, FileMode mode, FileAccess access, FileShare share) => _delegatee.FileOpen(path, mode, access, share);

            public StringComparison StringComparison => _delegatee.StringComparison;

            public bool FileExists(string fullPath) => _delegatee.FileExists(fullPath);
            public bool DirectoryExists(string fullPath) => _delegatee.DirectoryExists(fullPath);

            public string ReadAllText(string path) => _delegatee.ReadAllText(path);
            public byte[] FileReadAllBytes(string path) => _delegatee.FileReadAllBytes(path);
            public IEnumerable<string> FileReadAllLines(string path) => _delegatee.FileReadAllLines(path);

            public long FileSize(string path) => _delegatee.FileSize(path);
            public string[] GetDirectories(string path) => _delegatee.GetDirectories(path);
            public IDirectoryInfo GetDirectoryInfo(string directoryPath) => _delegatee.GetDirectoryInfo(directoryPath);
            public FileAttributes GetFileAttributes(string fullPath) => _delegatee.GetFileAttributes(fullPath);
            public string[] GetFiles(string path) => _delegatee.GetFiles(path);
            public string[] GetFiles(string path, string pattern) => _delegatee.GetFiles(path, pattern);
            public string[] GetFiles(string path, string pattern, SearchOption option) => _delegatee.GetFiles(path, pattern, option);
            public string[] GetFileSystemEntries(string path, string searchPattern, SearchOption options) => _delegatee.GetFileSystemEntries(path, searchPattern, options);
            public Version GetFileVersion(string path) => _delegatee.GetFileVersion(path);
            public DateTime GetLastWriteTimeUtc(string fullPath) => _delegatee.GetLastWriteTimeUtc(fullPath);
            public bool IsPathUnderRoot(string root, string path) => _delegatee.IsPathUnderRoot(root, path);

            public IFileSystemWatcher CreateFileSystemWatcher(string directory, string filter) => _delegatee.CreateFileSystemWatcher(directory, filter);
        }
    }
}
