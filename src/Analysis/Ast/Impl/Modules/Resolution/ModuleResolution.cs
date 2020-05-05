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
using System.Linq;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal static class ModuleResolution {
        public static bool TryCreateModuleStub(string name, string modulePath, IServiceContainer services, PathResolverSnapshot prs, out IPythonModule module) {
            // First check stub next to the module.
            var fs = services.GetService<IFileSystem>();
            if (!string.IsNullOrEmpty(modulePath)) {
                var pyiPath = Path.ChangeExtension(modulePath, "pyi");
                if (fs.FileExists(pyiPath)) {
                    module = new StubPythonModule(name, pyiPath, false, services);
                    return true;
                }
            }

            // Try location of stubs that are in a separate folder next to the package.
            var stubPath = prs.GetPossibleModuleStubPaths(name).FirstOrDefault(p => fs.FileExists(p));
            module = !string.IsNullOrEmpty(stubPath) ? new StubPythonModule(name, stubPath, false, services) : null;
            return module != null;
        }
    }
}
