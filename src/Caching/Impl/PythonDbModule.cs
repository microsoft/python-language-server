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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class PythonDbModule : SpecializedModule {
        private readonly NewLineLocation[] _newLines;
        private readonly int _fileSize;

        public PythonDbModule(ModuleModel model, string filePath, IServiceContainer services)
            : base(model.Name, filePath, services) {
            GlobalScope = new GlobalScope(model, this, services);
            Documentation = model.Documentation;

            _newLines = model.NewLines;
            _fileSize = model.FileSize;
        }

        protected override string LoadContent() => string.Empty;

        public override string Documentation { get; }
        public override IEnumerable<string> GetMemberNames() => GlobalScope.Variables.Names;

        #region ILocationConverter
        public override SourceLocation IndexToLocation(int index) => NewLineLocation.IndexToLocation(_newLines, index);
        public override int LocationToIndex(SourceLocation location) => NewLineLocation.LocationToIndex(_newLines, location, _fileSize);
        #endregion
    }
}
