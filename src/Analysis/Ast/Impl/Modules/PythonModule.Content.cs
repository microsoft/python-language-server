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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules {
    internal partial class PythonModule {
        public void Update(IEnumerable<DocumentChange> changes) {
            lock (AnalysisLock) {
                _parseCts?.Cancel();
                _parseCts = new CancellationTokenSource();

                _linkedParseCts?.Dispose();
                _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.CancellationToken, _parseCts.Token);

                _buffer.Update(changes);
                _updated = true;

                Parse();
            }

            Services.GetService<IPythonAnalyzer>().InvalidateAnalysis(this);
        }

        public void Reset(string content) {
            lock (AnalysisLock) {
                if (content != Content) {
                    ContentState = State.None;
                    InitializeContent(content, _buffer.Version + 1);
                }
            }

            Services.GetService<IPythonAnalyzer>().InvalidateAnalysis(this);
        }

        protected virtual string LoadContent() {
            if (ContentState < State.Loading) {
                ContentState = State.Loading;
                try {
                    var code = FileSystem.ReadTextWithRetry(FilePath);
                    ContentState = State.Loaded;
                    return code;
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return null; // Keep content as null so module can be loaded later.
        }

        private void InitializeContent(string content, int version) {
            lock (AnalysisLock) {
                LoadContent(content, version);

                var startParse = ContentState < State.Parsing && (_parsingTask == null || version > 0);
                if (startParse) {
                    Parse();
                }
            }
        }

        private void LoadContent(string content, int version) {
            if (ContentState < State.Loading) {
                try {
                    content = content ?? LoadContent();
                    _buffer.Reset(version, content);
                    ContentState = State.Loaded;
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
