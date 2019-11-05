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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.LanguageServer {
    public interface ICommandHandler {
        /// <summary>
        /// Returns commands it can handle
        /// </summary>
        ImmutableArray<string> SupportingCommands { get; }

        /// <summary>
        /// Handle requested command with the arguments
        /// </summary>
        /// <param name="command">command string</param>
        /// <param name="arguments">arguments for the command</param>
        /// <param name="cancellationToken"><see cref="CancellationToken" /></param>
        /// <returns>Return true if it handled the command or false if it didn't</returns>
        Task<bool> HandleAsync(IServiceContainer service, string command, object[] arguments, CancellationToken cancellationToken);
    }
}
