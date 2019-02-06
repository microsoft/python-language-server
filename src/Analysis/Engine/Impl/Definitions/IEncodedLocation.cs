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

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Tracks positions coming from multiple formats without having to resolve the location
    /// until much later.  We store a location resolver which can turn a location object back
    /// into a a line and column number.  Usually the resolver will be a PythonAst instance
    /// and the Location will be some Node. The PythonAst then provides the location and 
    /// we don't have to turn an index into line/column during the analysis.
    /// </summary>
    public interface IEncodedLocation: IEquatable<IEncodedLocation>, ICanExpire {
        ILocationResolver Resolver { get; }
        object Location { get; }
        ILocationInfo GetLocationInfo();
    }
}
