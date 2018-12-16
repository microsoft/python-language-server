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

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Represents document that can be analyzed asynchronously.
    /// </summary>
    internal interface IAnalyzable {
        /// <summary>
        /// Expected version of the analysis when asynchronous operations complete.
        /// Typically every change to the document or documents that depend on it
        /// increment the expected version. At the end of the analysis if the expected
        /// version is still the same, the analysis is applied to the document and
        /// becomes available to consumers.
        /// </summary>
        int ExpectedAnalysisVersion { get; }

        /// <summary>
        /// Notifies document that analysis is now pending. Typically document increments 
        /// the expected analysis version. The method can be called repeatedly without
        /// calling `CompleteAnalysis` first. The method is invoked for every dependency
        /// in the chain to ensure that objects know that their dependencies have been
        /// modified and the current analysis is no longer up to date.
        /// </summary>
        void NotifyAnalysisPending();

        /// <summary>
        /// Notifies document that its analysis is now complete.
        /// </summary>
        /// <param name="analysis">Document analysis</param>
        /// (version of the snapshot in the beginning of analysis).</param>
        /// <returns>True if analysis was accepted, false if is is out of date.</returns>
        bool NotifyAnalysisComplete(IDocumentAnalysis analysis);
    }
}
