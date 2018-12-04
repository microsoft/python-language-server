// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    public class AstPythonInterpreterFactory : IPythonInterpreterFactory, IPythonInterpreterFactoryWithLog, ICustomInterpreterSerialization, IDisposable {
        private readonly bool _useDefaultDatabase;
        private bool _disposed;

        private AnalysisLogWriter _log;
        // Available for tests to override
        internal static bool LogToConsole = false;

#if DEBUG
        const int LogCacheSize = 1;
        const int LogRotationSize = 16384;
#else
        const int LogCacheSize = 20;
        const int LogRotationSize = 4096;
#endif

        public AstPythonInterpreterFactory(InterpreterConfiguration config, InterpreterFactoryCreationOptions options)
            : this(config, options, string.IsNullOrEmpty(options?.DatabasePath)) { }

        private AstPythonInterpreterFactory(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options,
            bool useDefaultDatabase
        ) {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));
            CreationOptions = options ?? new InterpreterFactoryCreationOptions();
            try {
                LanguageVersion = Configuration.Version.ToLanguageVersion();
            } catch (InvalidOperationException ex) {
                throw new ArgumentException(ex.Message, ex);
            }

            _useDefaultDatabase = useDefaultDatabase;
            if (!string.IsNullOrEmpty(CreationOptions.DatabasePath) && CreationOptions.TraceLevel != TraceLevel.Off) {
                _log = new AnalysisLogWriter(Path.Combine(CreationOptions.DatabasePath, "AnalysisLog.txt"), false, LogToConsole, LogCacheSize);
                _log.Rotate(LogRotationSize);
                _log.MinimumLevel = CreationOptions.TraceLevel;
            }            
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AstPythonInterpreterFactory() {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                _disposed = true;
                _log?.Flush(synchronous: true);

                if (disposing) {
                    if (_log != null) {
                        _log.Dispose();
                    }
                }
            }
        }

        bool ICustomInterpreterSerialization.GetSerializationInfo(out string assembly, out string typeName, out Dictionary<string, object> properties) {
            assembly = GetType().Assembly.Location;
            typeName = GetType().FullName;
            properties = CreationOptions.ToDictionary();
            Configuration.WriteToDictionary(properties);
            if (_useDefaultDatabase) {
                properties["UseDefaultDatabase"] = true;
            }
            return true;
        }

        internal AstPythonInterpreterFactory(Dictionary<string, object> properties) :
            this(
                InterpreterConfiguration.FromDictionary(properties),
                InterpreterFactoryCreationOptions.FromDictionary(properties),
                properties.ContainsKey("UseDefaultDatabase")
            ) { }

        public InterpreterConfiguration Configuration { get; }

        public InterpreterFactoryCreationOptions CreationOptions { get; }

        public PythonLanguageVersion LanguageVersion { get; }

        public event EventHandler ImportableModulesChanged;

        public void NotifyImportNamesChanged() {
            ImportableModulesChanged?.Invoke(this, EventArgs.Empty);
        }

        public virtual IPythonInterpreter CreateInterpreter() 
            => new AstPythonInterpreter(this, _useDefaultDatabase, _log);

        internal void Log(TraceLevel level, string eventName, params object[] args) {
            _log?.Log(level, eventName, args);
        }
        
        public string GetAnalysisLogContent(IFormatProvider culture) {
            _log?.Flush(synchronous: true);
            var logfile = _log?.OutputFile;
            if (!File.Exists(logfile)) {
                return null;
            }

            try {
                return File.ReadAllText(logfile);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                return ex.ToString();
            }
        }
    }
}
