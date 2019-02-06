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
using System.Collections;

namespace Microsoft.Python.LanguageServer.Protocol {
    [Serializable]
    public sealed class LanguageServerException : Exception {
        public const int UnknownDocument = 1;
        public const int UnsupportedDocumentType = 2;
        public const int MismatchedVersion = 3;
        public const int UnknownExtension = 4;

        public int Code => (int)Data["Code"];

        public sealed override IDictionary Data => base.Data;

        public LanguageServerException(int code, string message) : base(message) {
            Data["Code"] = code;
        }

        public LanguageServerException(int code, string message, Exception innerException) : base(message, innerException) {
            Data["Code"] = code;
        }
    }
}
