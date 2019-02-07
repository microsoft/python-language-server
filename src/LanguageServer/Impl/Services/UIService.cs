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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Python.Core.Shell;
using Microsoft.Python.LanguageServer.Protocol;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Services {
    public sealed class UIService : IUIService {
        private readonly JsonRpc _rpc;

        public UIService(JsonRpc rpc) {
            _rpc = rpc;
        }

        public Task ShowMessageAsync(string message, TraceEventType eventType) {
            var parameters = new ShowMessageRequestParams {
                type = eventType.ToMessageType(),
                message = message
            };
            return _rpc.NotifyWithParameterObjectAsync("window/showMessage", parameters);
        }

        public async Task<string> ShowMessageAsync(string message, string[] actions, TraceEventType eventType) {
            var parameters = new ShowMessageRequestParams {
                type = eventType.ToMessageType(),
                message = message,
                actions = actions.Select(a => new MessageActionItem { title = a }).ToArray()
            };
            var result = await _rpc.InvokeWithParameterObjectAsync<MessageActionItem>("window/showMessageRequest", parameters);
            return result?.title;
        }

        public Task SetStatusBarMessageAsync(string message)
            => _rpc.NotifyWithParameterObjectAsync("window/setStatusBarMessage", message);
    }
}
