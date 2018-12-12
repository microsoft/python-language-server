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
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    public abstract class PythonModuleType : IPythonModule {
        protected IDictionary<string, IPythonType> Members { get; set; } = new Dictionary<string, IPythonType>();
        protected ILogger Log { get; }
        protected IFileSystem FileSystem { get; }
        protected IServiceContainer Services { get; }

        protected PythonModuleType(string name) {
            Check.ArgumentNotNull(nameof(name), name);
            Name = name;
        }

        protected PythonModuleType(string name, IServiceContainer services)
            : this(name) {
            Check.ArgumentNotNull(nameof(services), services);
            Services = services;
            FileSystem = services.GetService<IFileSystem>();
            Log = services.GetService<ILogger>();
        }

        protected PythonModuleType(string name, string filePath, Uri uri, IServiceContainer services)
            : this(name, services) {
            if (uri == null && !string.IsNullOrEmpty(filePath)) {
                Uri.TryCreate(filePath, UriKind.Absolute, out uri);
            }
            Uri = uri;
            FilePath = filePath ?? uri?.LocalPath;
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
        public virtual IEnumerable<string> GetChildrenModuleNames() => Enumerable.Empty<string>();
        #endregion

        #region IDisposable
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing) { }
        #endregion
    }
}
