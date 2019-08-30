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

namespace Microsoft.Python.Analysis.Core.Interpreter {
    public sealed class InterpreterConfiguration : IEquatable<InterpreterConfiguration> {
        /// <summary>
        /// Constructs a new interpreter configuration based on the provided values.
        /// </summary>
        public InterpreterConfiguration(string interpreterPath = null, Version version = null) :
            this(interpreterPath, string.Empty, null, null, default, version) { }

        // Tests only
        internal InterpreterConfiguration(
            string interpreterPath = null,
            string pathVar = "",
            string libPath = null,
            string sitePackagesPath = null,
            InterpreterArchitecture architecture = default,
            Version version = null
        ) {
            InterpreterPath = interpreterPath;
            PathEnvironmentVariable = pathVar;
            Architecture = architecture ?? InterpreterArchitecture.Unknown;
            Version = version ?? new Version();
            LibraryPath = libPath ?? string.Empty;
            SitePackagesPath = sitePackagesPath ?? string.Empty;
        }

        private static string Read(IReadOnlyDictionary<string, object> d, string k)
            => d.TryGetValue(k, out var o) ? o as string : null;

        private InterpreterConfiguration(IReadOnlyDictionary<string, object> properties) {
            InterpreterPath = Read(properties, nameof(InterpreterPath));
            PathEnvironmentVariable = Read(properties, nameof(PathEnvironmentVariable));
            LibraryPath = Read(properties, nameof(LibraryPath));
            Architecture = InterpreterArchitecture.TryParse(Read(properties, nameof(Architecture)));
            try {
                Version = Version.Parse(Read(properties, nameof(Version)));
            } catch (Exception ex) when (ex is ArgumentException || ex is FormatException) {
                Version = new Version();
            }
        }

        public void WriteToDictionary(IDictionary<string, object> properties) {
            properties[nameof(InterpreterPath)] = InterpreterPath;
            properties[nameof(PathEnvironmentVariable)] = PathEnvironmentVariable;
            properties[nameof(LibraryPath)] = LibraryPath;
            properties[nameof(Architecture)] = Architecture.ToString();
            if (Version != null) {
                properties[nameof(Version)] = Version.ToString();
            }
        }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python
        /// applications.
        /// </summary>
        public string InterpreterPath { get; }

        /// <summary>
        /// Gets the environment variable which should be used to set sys.path.
        /// </summary>
        public string PathEnvironmentVariable { get; }

        /// <summary>
        /// The architecture of the interpreter executable.
        /// </summary>
        public InterpreterArchitecture Architecture { get; }

        /// <summary>
        /// The language version of the interpreter (e.g. 2.7).
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Returns path to Python standard libraries.
        /// </summary>
        public string LibraryPath { get; }

        /// <summary>
        /// Returns path to Python site packages from 'import site; print(site.getsitepackages())'
        /// </summary>
        public string SitePackagesPath { get; }

        public static bool operator ==(InterpreterConfiguration x, InterpreterConfiguration y)
            => x?.Equals(y) ?? ReferenceEquals(y, null);
        public static bool operator !=(InterpreterConfiguration x, InterpreterConfiguration y)
            => !(x?.Equals(y) ?? ReferenceEquals(y, null));

        public override bool Equals(object obj) => Equals(obj as InterpreterConfiguration);

        public bool Equals(InterpreterConfiguration other) {
            if (other == null) {
                return false;
            }

            var cmp = StringComparer.OrdinalIgnoreCase;
            return
                cmp.Equals(InterpreterPath, other.InterpreterPath) &&
                cmp.Equals(PathEnvironmentVariable, other.PathEnvironmentVariable) &&
                Architecture == other.Architecture &&
                Version == other.Version;
        }

        public override int GetHashCode() {
            var cmp = StringComparer.OrdinalIgnoreCase;
            return
                cmp.GetHashCode(InterpreterPath ?? "") ^
                cmp.GetHashCode(PathEnvironmentVariable ?? "") ^
                Architecture.GetHashCode() ^
                Version.GetHashCode();
        }

        public override string ToString() => InterpreterPath;
    }
}
