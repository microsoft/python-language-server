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
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Modules {
    public sealed class ModuleCreationOptions {
        /// <summary>
        /// The name of the module.
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// Module content. Can be null if file path or URI are provided.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// The path to the file on disk. Can be null if URI is provided.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Document URI. Can be null if file path is provided.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Module type (user, library, compiled, stub, ...).
        /// </summary>
        public ModuleType ModuleType { get; set; } = ModuleType.User;

        /// <summary>
        /// Module stub, if any.
        /// </summary>
        public IPythonModule Stub { get; set; }
    }
}
