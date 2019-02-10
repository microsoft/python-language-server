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
using Microsoft.Python.Core.Services;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Services {
    internal sealed class ClientApplication : IClientApplication {
        private readonly JsonRpc _rpc;

        public ClientApplication(JsonRpc rpc) {
            _rpc = rpc;
        }

        public Task NotifyAsync(string targetName, params object[] arguments)
            => _rpc.NotifyAsync(targetName, arguments);

        public Task NotifyWithParameterObjectAsync(string targetName, object argument = null)
            => _rpc.NotifyWithParameterObjectAsync(targetName, argument);

        public Task<TResult> InvokeWithParameterObjectAsync<TResult>(string targetName, object argument = null, CancellationToken cancellationToken = default)
            => _rpc.InvokeWithParameterObjectAsync<TResult>(targetName, argument, cancellationToken);
    }
}
