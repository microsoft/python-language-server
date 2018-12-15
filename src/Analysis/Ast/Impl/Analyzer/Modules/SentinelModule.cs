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

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal sealed class SentinelModule : PythonModule {
        private readonly TaskCompletionSource<IPythonModule> _tcs;
        private volatile IPythonModule _realModule;

        public SentinelModule(string name, bool importing): base(name, ModuleType.Empty, null) {
            if (importing) {
                _tcs = new TaskCompletionSource<IPythonModule>();
            } else {
                _realModule = this;
            }
        }

        public Task<IPythonModule> WaitForImportAsync(CancellationToken cancellationToken) 
            => _realModule != null ? Task.FromResult(_realModule) : _tcs.Task;

        public void Complete(IPythonModule module) { 
            if (_realModule == null) {
                _realModule = module;
                _tcs.TrySetResult(module);
            }
        }
    }
}
