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
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class ModuleMembersFilter {
        public static IEnumerable<string> GetMemberNames(this IPythonModule module, IFileSystem fs) {
            if(!(module is IDocument document) || document.Analysis?.GlobalScope?.Variables == null) {
                return module.GetMemberNames();
            }

            var thisModuleDirectory = Path.GetDirectoryName(module.FilePath);
            // drop imported modules and typing.
            return document.Analysis.GlobalScope.Variables
                .Where(v => {
                    // Instances are always fine.
                    if (v.Value is IPythonInstance) {
                        return true;
                    }

                    var valueType = v.Value?.GetPythonType();
                    switch (valueType) {
                        case IPythonModule m:
                            // Do not show modules except submodules.
                            return !string.IsNullOrEmpty(m.FilePath) && fs.IsPathUnderRoot(thisModuleDirectory, Path.GetDirectoryName(m.FilePath));
                        case IPythonFunctionType f when f.IsLambda():
                            return false;
                    }

                    if (module.IsTypingModule()) {
                        return true; // Let typing module behave normally.
                    }

                    // Do not re-export types from typing. However, do export variables
                    // assigned with types from typing. Example:
                    //    from typing import Any # do NOT export Any
                    //    x = Union[int, str] # DO export x
                    return valueType?.DeclaringModule.IsTypingModule() != true || v.Name != valueType.Name;
                })
                .Select(v => v.Name);
        }
    }
}
