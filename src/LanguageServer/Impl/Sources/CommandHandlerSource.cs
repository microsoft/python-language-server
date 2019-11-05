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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.LanguageServer.CodeActions;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed partial class CommandHandlerSource {
        private static readonly ImmutableArray<ICommandHandler> _handlers =
            ImmutableArray<ICommandHandler>.Create(StubGenerationRefactoringCodeActionProvider.Instance);

        private readonly IServiceContainer _services;

        public CommandHandlerSource(IServiceContainer services) {
            _services = services;
        }

        public async Task<object> HandleAsync(string command, object[] arguments, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var handler in _handlers) {
                if (!handler.SupportingCommands.Any(c => c == command)) {
                    continue;
                }

                if (await handler.HandleAsync(_services, command, arguments, cancellationToken)) {
                    // if this handler handled the command, exit otherwise, give next handler a chance to handle it
                    break;
                }
            }

            // nothing to return for now
            return null;
        }
    }

    internal static class WellKnownCommands {
        public const string StubGeneration = "python.command.generation.stub";

        public static readonly string[] Commands = new[] { StubGeneration };
    }
}
