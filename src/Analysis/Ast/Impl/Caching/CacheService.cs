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

using System.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.Analysis.Caching {
    /// <summary>
    /// Register caching services for the given cache folder path 
    /// </summary>
    public static class CacheService {
        public static void Register(IServiceManager services, string cacheFolderPath) {
            var log = services.GetService<ILogger>();
            var fs = services.GetService<IFileSystem>();

            // this is not thread safe. this is not supposed to be called concurrently
            var cachingService = services.GetService<ICacheFolderService>();
            if (cachingService != null) {
                return;
            }

            if (cacheFolderPath != null && !fs.DirectoryExists(cacheFolderPath)) {
                log?.Log(TraceEventType.Warning, Resources.Specified_cache_folder_0_does_not_exist_Switching_to_default.FormatUI(cacheFolderPath));
                cacheFolderPath = null;
            }

            services.AddService(new CacheFolderService(services, cacheFolderPath));
            services.AddService(new StubCache(services));
        }
    }
}
