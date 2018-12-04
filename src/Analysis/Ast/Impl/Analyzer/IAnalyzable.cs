using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Represents document that can be analyzed asynchronously.
    /// </summary>
    public interface IAnalyzable {
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
        /// calling `CompleteAnalysis` first.
        /// </summary>
        void NotifyAnalysisPending();

        /// <summary>
        /// Notifies document that its analysis is now complete.
        /// </summary>
        /// <param name="analysis">Document analysis</param>
        /// <returns>True if analysis was accepted, false if is is out of date.</returns>
        bool NotifyAnalysisComplete(IDocumentAnalysis analysis, int analysisVersion);
    }
}
