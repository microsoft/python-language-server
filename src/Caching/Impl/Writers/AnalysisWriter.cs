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

using System.IO;
using LiteDB;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Writers {
    internal sealed class AnalysisWriter {
        private readonly IServiceContainer _services;

        public AnalysisWriter(IServiceContainer services) {
            _services = services;
        }

        public void StoreModuleAnalysis(string moduleId, IDocumentAnalysis analysis) {
            if(!(analysis is DocumentAnalysis)) {
                return;
            }

            var cfs = _services.GetService<ICacheFolderService>();
            using (var db = new LiteDatabase(Path.Combine(cfs.CacheFolder, "Analysis.db"))) {
                var mw = new ModuleWriter();
                mw.WriteModule(db, moduleId, analysis);
            }
        }
    }
}

