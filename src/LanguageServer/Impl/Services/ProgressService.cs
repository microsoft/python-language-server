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

using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.LanguageServer.Services {
    public sealed class ProgressService : IProgressService {
        private readonly IClientApplication _clientApp;
        public ProgressService(IClientApplication clientApp) {
            _clientApp = clientApp;
        }

        public IProgress BeginProgress() => new Progress(_clientApp);

        private class Progress : IProgress {
            private readonly IClientApplication _clientApp;
            public Progress(IClientApplication clientApp) {
                _clientApp = clientApp;
                _clientApp.NotifyAsync("python/beginProgress").DoNotWait();
            }
            public Task Report(string message) => _clientApp.NotifyAsync("python/reportProgress", message);
            public void Dispose() => _clientApp.NotifyAsync("python/endProgress").DoNotWait();
        }
    }
}
