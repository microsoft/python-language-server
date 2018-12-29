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
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values {
    internal class PythonSequence : PythonInstance, IPythonSequence {
        private readonly IPythonInterpreter _interpreter;
        private readonly IMember _contentType;
        private readonly IReadOnlyList<IMember> _contentTypes;

        /// <summary>
        /// Creates sequence with consistent content (i.e. all strings)
        /// </summary>
        /// <param name="type">Sequence type.</param>
        /// <param name="contentType">Content type (str, int, ...).</param>
        /// <param name="interpreter">Python interpreter.</param>
        /// <param name="location">Declaring location.</param>
        public PythonSequence(IPythonType type, IMember contentType, IPythonInterpreter interpreter, LocationInfo location = null)
            : base(type, location) {
            _interpreter = interpreter;
            _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        /// <summary>
        /// Creates sequence with consistent content (i.e. all strings)
        /// </summary>
        /// <param name="typeName">Sequence type name.</param>
        /// <param name="sequenceTypeId">Sequence type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="contentType">Content type (str, int, ...).</param>
        /// <param name="interpreter">Python interpreter.</param>
        /// <param name="location">Declaring location.</param>
        public PythonSequence(string typeName, BuiltinTypeId sequenceTypeId, IMember contentType, IPythonInterpreter interpreter, LocationInfo location = null)
            : base(new PythonSequenceType(typeName, sequenceTypeId, interpreter), location) {
            _interpreter = interpreter;
            _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        /// <summary>
        /// Creates sequence with mixed content.
        /// </summary>
        /// <param name="typeName">Sequence type name.</param>
        /// <param name="sequenceTypeId">Sequence type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="contentTypes">Content types of the sequence elements (str, int, ...).</param>
        /// <param name="interpreter">Python interpreter.</param>
        /// <param name="location">Declaring location.</param>
        public PythonSequence(string typeName, BuiltinTypeId sequenceTypeId, IEnumerable<IMember> contentTypes, IPythonInterpreter interpreter, LocationInfo location = null)
            : base(new PythonSequenceType(typeName, sequenceTypeId, interpreter), location) {
            _interpreter = interpreter;
            _contentTypes = contentTypes?.ToArray() ?? throw new ArgumentNullException(nameof(contentTypes));
        }

        public IMember GetValueAt(int index) {
            if (_contentType != null) {
                return _contentType;
            }

            if (index < 0) {
                index = _contentTypes.Count + index; // -1 means last, etc.
            }

            if (_contentTypes != null && index >= 0 && index < _contentTypes.Count) {
                return _contentTypes[index];
            }

            return _interpreter.GetBuiltinType(BuiltinTypeId.Unknown);
        }

        public IEnumerable<IMember> GetContents() => _contentTypes ?? new[] {_contentType};

        public IPythonIterator GetIterator() => new PythonIterator(this);
    }
}
