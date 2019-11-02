// Python Tools for Visual Studio
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
using System.Runtime;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.LanguageServer.Optimization {
    internal sealed class ProfileOptimizationService : IProfileOptimizationService {
        public ProfileOptimizationService(IServiceContainer services) {
            var fileSystem = services.GetService<IFileSystem>();
            var cacheService = services.GetService<ICacheFolderService>();
            if (fileSystem == null || cacheService == null) {
                return;
            }

            try {
                // create directory for profile optimization
                var path = Path.Combine(cacheService.CacheFolder, "Profiles");
                fileSystem.CreateDirectory(path);

                ProfileOptimization.SetProfileRoot(path);
            } catch {
                // ignore any issue with profiling
            }
        }

        public void Profile(string name) {
            ProfileOptimization.StartProfile(name);
        }
    }
}
