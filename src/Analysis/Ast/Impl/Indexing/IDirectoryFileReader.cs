using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Python.Analysis.Indexing {
    public interface IDirectoryFileReader {
        IEnumerable<string> DirectoryFilePaths();
    }
}
