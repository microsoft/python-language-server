using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal class PossibleModuleImport : IImportSearchResult {
        public string PossibleModuleFullName { get; }
        public string RootPath { get; }
        public string PrecedingModuleFullName { get; }
        public string ExistingModuleModulePath { get; }
        public IReadOnlyList<string> RemainingNameParts { get; }

        public PossibleModuleImport(string possibleModuleFullName, string rootPath, string precedingModuleFullName, string existingModuleModulePath, IReadOnlyList<string> remainingNameParts) {
            PossibleModuleFullName = possibleModuleFullName;
            RootPath = rootPath;
            PrecedingModuleFullName = precedingModuleFullName;
            ExistingModuleModulePath = existingModuleModulePath;
            RemainingNameParts = remainingNameParts;
        }
    }
}
