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
// using System;

namespace Microsoft.Python.Analysis.Caching {
    /// <summary>
    /// Provides location of the analysis database cache.
    /// </summary>
    public interface IModuleDatabaseCache {
        /// <summary>
        /// Cache folder base name without version, such as 'analysis.v'.
        /// </summary>
        string CacheFolderBaseName { get; }

        /// <summary>
        /// Database format version.
        /// </summary>
        int DatabaseFormatVersion { get; }

        /// <summary>
        /// Full path to the cache folder includding version.
        /// </summary>
        string CacheFolder { get; }
    }
}
