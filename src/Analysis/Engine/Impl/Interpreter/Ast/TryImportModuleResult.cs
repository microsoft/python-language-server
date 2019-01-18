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

namespace Microsoft.PythonTools.Interpreter.Ast {
    public enum TryImportModuleResultCode {
        Success,
        ModuleNotFound,
        NeedRetry,
        NotSupported,
        Timeout
    }

    public struct TryImportModuleResult {
        public readonly TryImportModuleResultCode Status;
        public readonly IPythonModule Module;

        public TryImportModuleResult(IPythonModule module) {
            Status = module == null ? TryImportModuleResultCode.ModuleNotFound : TryImportModuleResultCode.Success;
            Module = module;
        }

        public TryImportModuleResult(TryImportModuleResultCode status) {
            Status = status;
            Module = null;
        }

        public static TryImportModuleResult ModuleNotFound => new TryImportModuleResult(TryImportModuleResultCode.ModuleNotFound);
        public static TryImportModuleResult NeedRetry => new TryImportModuleResult(TryImportModuleResultCode.NeedRetry);
        public static TryImportModuleResult NotSupported => new TryImportModuleResult(TryImportModuleResultCode.NotSupported);
        public static TryImportModuleResult Timeout => new TryImportModuleResult(TryImportModuleResultCode.Timeout);
    }

}
