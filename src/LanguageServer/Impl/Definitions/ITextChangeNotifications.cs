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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.LanguageServer.Definitions {
    public interface IDocumentChangeNotifications {
        Task DidChangeConfiguration(DidChangeConfigurationParams @params);
        Task DidOpenTextDocument(DidOpenTextDocumentParams @params, CancellationToken token);
        Task DidChangeTextDocument(DidChangeTextDocumentParams @params, CancellationToken token);
        Task DidSaveTextDocument(DidSaveTextDocumentParams @params, CancellationToken token);
        Task DidCloseTextDocument(DidCloseTextDocumentParams @params, CancellationToken token);
    }
}
