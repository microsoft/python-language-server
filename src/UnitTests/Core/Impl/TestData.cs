﻿// Visual Studio Shared Project
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
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Tests.Utilities;

namespace TestUtilities {
    public static class TestData {
        private static readonly AsyncLocal<TestRunScope> TestRunScopeAsyncLocal = new AsyncLocal<TestRunScope>();

        private static string GetRootDir() {
            var dir = PathUtils.GetParent(typeof(TestData).Assembly.Location);
            while (!string.IsNullOrEmpty(dir) &&
                Directory.Exists(dir) &&
                !File.Exists(PathUtils.GetAbsoluteFilePath(dir, "build.root"))) {
                dir = PathUtils.GetParent(dir);
            }
            return dir ?? "";
        }

        /// <summary>
        /// Returns the full path to the test data root.
        /// </summary>
        private static string CalculateTestDataRoot() {
            var path = Environment.GetEnvironmentVariable("_TESTDATA_ROOT_PATH");
            if (Directory.Exists(path)) {
                return path;
            }

            path = GetRootDir();
            if (Directory.Exists(path)) {
                foreach (var landmark in new[] {
                    "TestData",
                    Path.Combine("src", "UnitTests", "TestData")
                }) {
                    var candidate = PathUtils.GetAbsoluteDirectoryPath(path, landmark);
                    if (Directory.Exists(candidate)) {
                        return PathUtils.GetParent(candidate);
                    }
                }
            }

            throw new InvalidOperationException("Failed to find test data");
        }

        private static readonly Lazy<string> RootLazy = new Lazy<string>(CalculateTestDataRoot);
        public static string Root => RootLazy.Value;

        public static string GetDefaultTypeshedPath() {
            var asmPath = Assembly.GetExecutingAssembly().GetAssemblyPath();
            return Path.Combine(Path.GetDirectoryName(asmPath), "Typeshed");
        }

        public static Uri GetDefaultModuleUri() => new Uri(GetDefaultModulePath());
        public static Uri GetNextModuleUri() => new Uri(GetNextModulePath());
        public static Uri[] GetNextModuleUris(int count) {
            var uris = new Uri[count];
            for (var i = 0; i < count; i++) {
                uris[i] = GetNextModuleUri();
            }
            return uris;
        }

        public static Uri GetTestSpecificUri(string relativePath) => new Uri(GetTestSpecificPath(relativePath));
        public static Uri GetTestSpecificUri(params string[] parts) => new Uri(GetTestSpecificPath(parts));
        public static Uri GetTestSpecificRootUri() => TestRunScopeAsyncLocal.Value.RootUri;

        public static string GetTestSpecificPath(string relativePath) => TestRunScopeAsyncLocal.Value.GetTestSpecificPath(relativePath);
        public static string GetTestSpecificPath(params string[] parts) => TestRunScopeAsyncLocal.Value.GetTestSpecificPath(Path.Combine(parts));
        public static string GetTestRelativePath(Uri uri) => TestRunScopeAsyncLocal.Value.GetTestRelativePath(uri);
        public static string GetTestSpecificRootPath() => TestRunScopeAsyncLocal.Value.Root;
        public static string GetDefaultModulePath() => TestRunScopeAsyncLocal.Value.GetDefaultModulePath();
        public static string GetNextModulePath() => TestRunScopeAsyncLocal.Value.GetNextModulePath();

        public static string GetAstAnalysisCachePath(Version version, bool testSpecific = false, string prefix = null) {
            var cacheRoot = prefix != null ? $"{prefix}{version}" : $"AstAnalysisCache{version}";
            return testSpecific ? TestRunScopeAsyncLocal.Value.GetTestSpecificPath(cacheRoot) : GetTestOutputRootPath(cacheRoot);
        }

        public static async Task<Uri> CreateTestSpecificFileAsync(string relativePath, string content) {
            var path = GetTestSpecificPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = File.Create(path)) {
                var contentBytes = Encoding.Default.GetBytes(content);
                await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
            }
            return new Uri(path);
        }

        /// <summary>
        /// Returns the full path to the deployed file.
        /// </summary>
        public static string GetPath(params string[] paths) {
            var res = Root;
            foreach (var p in paths) {
                res = PathUtils.GetAbsoluteFilePath(res, p);
            }
            return res;
        }

        private static string CalculateTestOutputRoot() {
            var path = Environment.GetEnvironmentVariable("_TESTDATA_TEMP_PATH");

            if (string.IsNullOrEmpty(path)) {
                path = Path.Combine(GetRootDir(), "TestResults", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            }

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        private static readonly Lazy<string> TestOutputRootLazy = new Lazy<string>(CalculateTestOutputRoot);

        /// <summary>
        /// Returns the full path to a temporary directory in the test output root.
        /// This is within the deployment to ensure that test files are easily cleaned up.
        /// </summary>
        private static string GetTestOutputRootPath(string subPath = null) {
            var path = PathUtils.GetAbsoluteDirectoryPath(TestOutputRootLazy.Value, subPath);
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            Console.WriteLine($"Creating temp directory for test at {path}");
            return path;
        }

        internal static void SetTestRunScope(string testFullName) {
            var testDirectoryName = testFullName ?? Path.GetRandomFileName();
            var path = Path.Combine(TestOutputRootLazy.Value, testDirectoryName);

            Directory.CreateDirectory(path);
            TestRunScopeAsyncLocal.Value = new TestRunScope(path);
        }

        internal static void ClearTestRunScope() {
            TestRunScopeAsyncLocal.Value = null;
        }
    }

    internal class TestRunScope {
        private int _moduleCounter;
        public string Root { get; }
        public Uri RootUri { get; }

        public TestRunScope(string root) {
            Root = root;
            RootUri = new Uri(Root);
        }

        public string GetDefaultModulePath() => GetTestSpecificPath($"module.py");
        public string GetNextModulePath() => GetTestSpecificPath($"module{++_moduleCounter}.py");
        public string GetTestSpecificPath(string relativePath) => Path.Combine(Root, relativePath);
        public string GetTestRelativePath(Uri uri) {
            var relativeUri = RootUri.MakeRelativeUri(uri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());
            return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }
}

