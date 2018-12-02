﻿// Python Tools for Visual Studio
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public interface IPythonAnalyzer {
        PythonLanguageVersion LanguageVersion { get; }

        /// <summary>
        /// Reloads the modules from the interpreter.
        /// 
        /// This method should be called on the analysis thread and is usually invoked
        /// when the interpreter signals that it's modules have changed.
        /// </summary>
        Task ReloadModulesAsync(CancellationToken token = default);

        /// <summary>
        /// Adds a new user file to the list of available modules and returns a ProjectEntry object.
        /// </summary>
        /// <param name="moduleName">The name of the module; used to associate with imports</param>
        /// <param name="filePath">The path to the file on disk</param>
        /// <param name="documentUri">Document URI.</param>
        /// <returns>The project entry for the new module.</returns>
        IProjectEntry AddModule(string moduleName, string filePath, Uri documentUri = null);

        /// <summary>
        /// Removes the specified project entry from the current analysis.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="onImporter">Action to perform on each module that
        /// had imported the one being removed.</param>
        void RemoveModule(IProjectEntry entry, Action<IPythonProjectEntry> onImporter = null);

        /// <summary>
        /// Returns a sequence of project entries that import the specified
        /// module. The sequence will be empty if the module is unknown.
        /// </summary>
        /// <param name="moduleName">
        /// The absolute name of the module. This should never end with
        /// '__init__'.
        /// </param>
        IEnumerable<IPythonProjectEntry> GetEntriesThatImportModule(string moduleName, bool includeUnresolved);

        AnalysisValue GetAnalysisValueFromObjects(object attr);

        /// <summary>
        /// Returns true if a module has been imported.
        /// </summary>
        /// <param name="importFrom">
        /// The entry of the module doing the import. If null, the module name
        /// is resolved as an absolute name.
        /// </param>
        /// <param name="relativeModuleName">
        /// The absolute or relative name of the module. If a relative name is 
        /// passed here, <paramref name="importFrom"/> must be provided.
        /// </param>
        /// <param name="absoluteImports">
        /// True if Python 2.6/3.x style imports should be used.
        /// </param>
        /// <returns>
        /// True if the module was imported during analysis; otherwise, false.
        /// </returns>
        bool IsModuleResolved(IPythonProjectEntry importFrom, string relativeModuleName, bool absoluteImports);

        /// <summary>
        /// Gets a top-level list of all the available modules as a list of MemberResults.
        /// </summary>
        IMemberResult[] GetModules();

        /// <summary>
        /// Searches all modules which match the given name and searches in the modules
        /// for top-level items which match the given name.  Returns a list of all the
        /// available names fully qualified to their name.  
        /// </summary>
        /// <param name="name"></param>
        IEnumerable<ExportedMemberInfo> FindNameInAllModules(string name);

        /// <summary>
        /// Returns the interpreter that the analyzer is using.
        /// This property is thread safe.
        /// </summary>
        IPythonInterpreter Interpreter { get; }

        /// <summary>
        /// Returns the interpreter factory that the analyzer is using.
        /// </summary>
        IPythonInterpreterFactory InterpreterFactory { get; }

        /// <summary>
        /// returns the MemberResults associated with modules in the specified
        /// list of names.  The list of names is the path through the module, for example
        /// ['System', 'Runtime']
        /// </summary>
        /// <returns></returns>
        IMemberResult[] GetModuleMembers(IModuleContext moduleContext, string[] names, bool includeMembers = false);

        /// <summary>
        /// Gets the list of directories which should be analyzed.
        /// This property is thread safe.
        /// </summary>
        IEnumerable<string> AnalysisDirectories { get; }

        /// <summary>
        /// Gets the list of directories which should be searched for type stubs.
        /// This property is thread safe.
        /// </summary>
        IEnumerable<string> TypeStubDirectories { get; }

        AnalysisLimits Limits { get; set; }
        bool EnableDiagnostics { get; set; }
        void AddDiagnostic(Node node, AnalysisUnit unit, string message, DiagnosticSeverity severity, string code = null);
        IReadOnlyList<Diagnostic> GetDiagnostics(IProjectEntry entry);
        IReadOnlyDictionary<IProjectEntry, IReadOnlyList<Diagnostic>> GetAllDiagnostics();
        void ClearDiagnostic(Node node, AnalysisUnit unit, string code = null);
        void ClearDiagnostics(IProjectEntry entry);
    }
}
