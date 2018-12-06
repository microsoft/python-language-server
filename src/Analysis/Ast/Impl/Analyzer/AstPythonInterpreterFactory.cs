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
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    public class AstPythonInterpreterFactory : IPythonInterpreterFactory {
        private readonly bool _useDefaultDatabase;
        private readonly ILogger _log;

        public AstPythonInterpreterFactory(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options,
            ILogger log
            ) : this(config, options, string.IsNullOrEmpty(options?.DatabasePath), log) { }

        private AstPythonInterpreterFactory(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options,
            bool useDefaultDatabase,
            ILogger log
        ) {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));
            CreationOptions = options ?? new InterpreterFactoryCreationOptions();
            try {
                LanguageVersion = Configuration.Version.ToLanguageVersion();
            } catch (InvalidOperationException ex) {
                throw new ArgumentException(ex.Message, ex);
            }

            _log = log;
            _useDefaultDatabase = useDefaultDatabase;
        }

        public InterpreterConfiguration Configuration { get; }

        public InterpreterFactoryCreationOptions CreationOptions { get; }

        public PythonLanguageVersion LanguageVersion { get; }

        public event EventHandler ImportableModulesChanged;

        public void NotifyImportNamesChanged() => ImportableModulesChanged?.Invoke(this, EventArgs.Empty);

        public virtual IPythonInterpreter CreateInterpreter() => new AstPythonInterpreter(this, _useDefaultDatabase, _log);
    }
}
