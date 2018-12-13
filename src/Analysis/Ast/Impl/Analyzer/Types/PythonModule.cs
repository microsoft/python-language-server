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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    public abstract class PythonModule : IPythonModule, ILocatedMember {
        protected IDictionary<string, IPythonType> Members { get; set; } = new Dictionary<string, IPythonType>();
        protected ILogger Log { get; }
        protected IFileSystem FileSystem { get; }
        protected IServiceContainer Services { get; }

        protected PythonModule(string name) {
            Check.ArgumentNotNull(nameof(name), name);
            Name = name;
        }

        protected PythonModule(string name, IServiceContainer services)
            : this(name) {
            Check.ArgumentNotNull(nameof(services), services);
            Services = services;
            FileSystem = services.GetService<IFileSystem>();
            Log = services.GetService<ILogger>();
            Locations = Array.Empty<LocationInfo>();
        }

        protected PythonModule(string name, string filePath, Uri uri, IServiceContainer services)
            : this(name, services) {
            if (uri == null && !string.IsNullOrEmpty(filePath)) {
                Uri.TryCreate(filePath, UriKind.Absolute, out uri);
            }
            Uri = uri;
            FilePath = filePath ?? uri?.LocalPath;
            Locations = new[] { new LocationInfo(filePath, uri, 1, 1) };
        }

        #region IPythonType
        public string Name { get; }
        public virtual string Documentation { get; } = string.Empty;

        public virtual IPythonModule DeclaringModule => null;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltin => true;
        public bool IsTypeFactory => false;
        public IPythonFunction GetConstructor() => null;
        public PythonMemberType MemberType => PythonMemberType.Module;
        #endregion

        #region IMemberContainer
        public virtual IPythonType GetMember(string name) => Members.TryGetValue(name, out var m) ? m : null;
        public virtual IEnumerable<string> GetMemberNames() => Members.Keys.ToArray();
        #endregion

        #region IPythonFile

        public virtual string FilePath { get; }
        public virtual Uri Uri { get; }
        public virtual IPythonInterpreter Interpreter { get; }
        #endregion

        #region IPythonModule
        [DebuggerStepThrough]
        public virtual IEnumerable<string> GetChildrenModuleNames() => GetChildModuleNames(FilePath, Name, Interpreter);

        private IEnumerable<string> GetChildModuleNames(string filePath, string prefix, IPythonInterpreter interpreter) {
            if (interpreter == null || string.IsNullOrEmpty(filePath)) {
                yield break;
            }
            var searchPath = Path.GetDirectoryName(filePath);
            if (!FileSystem.DirectoryExists(searchPath)) {
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
        #endregion

        #region ILocatedMember
        public IEnumerable<LocationInfo> Locations { get; }
        #endregion
    }
}
