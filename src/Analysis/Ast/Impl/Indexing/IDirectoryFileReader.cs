using System.Collections.Generic;

namespace Microsoft.Python.Analysis.Indexing {
    public interface IDirectoryFileReader {
        IEnumerable<string> DirectoryFilePaths();
    }
}
