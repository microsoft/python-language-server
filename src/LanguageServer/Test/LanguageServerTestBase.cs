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

using System;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Tests;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.LanguageServer.Diagnostics;
using NSubstitute;

namespace Microsoft.Python.LanguageServer.Tests {
    public abstract class LanguageServerTestBase : AnalysisTestBase {
        protected static readonly ServerSettings ServerSettings = new ServerSettings();
        protected override IDiagnosticsService GetDiagnosticsService(IServiceContainer s) => new DiagnosticsService(s);


        protected IDiagnosticsService GetDiagnosticsService() {
            var ds = Services.GetService<IDiagnosticsService>();
            ds.PublishingDelay = 0;
            return ds;
        }
        protected void PublishDiagnostics() {
            GetDiagnosticsService();
            RaiseIdleEvent();
        }

        protected void RaiseIdleEvent() {
            var idle = Services.GetService<IIdleTimeService>();
            idle.Idle += Raise.EventWith(null, EventArgs.Empty);
        }
    }
}
