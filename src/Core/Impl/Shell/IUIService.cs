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
using System.Threading.Tasks;

namespace Microsoft.Python.Core.Shell {
    /// <summary>
    /// Service that represents the application user interface.
    /// </summary>
    public interface IUIService {
        /// <summary>
        /// Displays error message in a host-specific UI
        /// </summary>
        Task ShowMessageAsync(string message, TraceEventType eventType);

        /// <summary>
        /// Displays message with specified buttons in a host-specific UI
        /// </summary>
        Task<string> ShowMessageAsync(string message, string[] actions, TraceEventType eventType);

        /// <summary>
        /// Writes message to the host application status bar
        /// </summary>
        /// <param name="message"></param>
        Task SetStatusBarMessageAsync(string message);
    }
}
