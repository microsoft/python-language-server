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
using System.IO;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    public class AstPythonInterpreterFactory : IPythonInterpreterFactory {
        public AstPythonInterpreterFactory(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options,
            ILogger log
        ) {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));
            CreationOptions = options ?? new InterpreterFactoryCreationOptions();
            try {
                LanguageVersion = Configuration.Version.ToLanguageVersion();
            } catch (InvalidOperationException ex) {
                throw new ArgumentException(ex.Message, ex);
            }

            Log = log;

            UseDefaultDatabase = string.IsNullOrEmpty(options?.DatabasePath);
            if (UseDefaultDatabase) {
                var dbPath = Path.Combine("DefaultDB", $"v{Configuration.Version.Major}", "python.pyi");
                if (InstallPath.TryGetFile(dbPath, out var biPath)) {
                    DatabasePath = Path.GetDirectoryName(biPath);
                }
            }
        }

        public InterpreterConfiguration Configuration { get; }

        public InterpreterFactoryCreationOptions CreationOptions { get; }

        public PythonLanguageVersion LanguageVersion { get; }

        public ILogger Log { get; }
        public string SearchPathCachePath => Path.Combine(CreationOptions.DatabasePath, "database.path");
        public string DatabasePath { get; }
        public bool UseDefaultDatabase { get; }

        public virtual IPythonInterpreter CreateInterpreter() => new AstPythonInterpreter(this);
    }
}
