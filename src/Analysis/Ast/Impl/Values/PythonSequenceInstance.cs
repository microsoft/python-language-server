﻿// Copyright(c) Microsoft Corporation
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
    internal sealed class PythonSequenceInstance : PythonInstance, IPythonSequenceInstance {
        private readonly IPythonInterpreter _interpreter;
        private readonly IMember _contentType;
        private readonly IReadOnlyList<IMember> _contentTypes;

        /// <summary>
        /// Creates list with consistent content (i.e. all strings)
        /// </summary>
        public PythonSequenceInstance(IMember contentType, IPythonInterpreter interpreter, LocationInfo location = null)
            : base(interpreter.GetBuiltinType(BuiltinTypeId.List), location) {
            _interpreter = interpreter;
            _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        /// <summary>
        /// Creates list with mixed content.
        /// </summary>
        public PythonSequenceInstance(IEnumerable<IMember> contentTypes, IPythonInterpreter interpreter, LocationInfo location = null)
            : base(interpreter.GetBuiltinType(BuiltinTypeId.List), location) {
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
    }
}
