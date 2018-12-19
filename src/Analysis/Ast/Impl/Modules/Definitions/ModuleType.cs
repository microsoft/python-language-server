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

namespace Microsoft.Python.Analysis.Modules {
    [Flags]
    public enum ModuleType {
        /// <summary>
        /// Module is user file in the workspace.
        /// </summary>
        User,

        /// <summary>
        /// Module is library module in Python.
        /// </summary>
        Library,

        /// <summary>
        /// Module is a stub module.
        /// </summary>
        Stub,

        /// <summary>
        /// Module source was scraped from a compiled module.
        /// </summary>
        Compiled,

        /// <summary>
        /// Module source was scraped from a built-in compiled module.
        /// </summary>
        CompiledBuiltin,

        /// <summary>
        /// Module is the Python 'builtins' module.
        /// </summary>
        Builtins,

        /// <summary>
        /// Module that contains child modules
        /// </summary>
        Package,

        /// <summary>
        /// Unresolved import.
        /// </summary>
        Unresolved
    }
}
