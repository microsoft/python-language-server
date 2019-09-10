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
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Core;
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    internal abstract class CallableModel : MemberModel {
        public string Documentation { get; set; }
        public FunctionAttributes Attributes { get; set; }
        public ClassModel[] Classes { get; set; }
        public FunctionModel[] Functions { get; set; }

        [NonSerialized]
        private readonly ReentrancyGuard<IMember> _processing = new ReentrancyGuard<IMember>();
        protected CallableModel() { } // For de-serializer from JSON

        protected CallableModel(IPythonType callable) {
            var functions = new List<FunctionModel>();
            var classes = new List<ClassModel>();

            foreach (var name in callable.GetMemberNames()) {
                var m = callable.GetMember(name);

                // Only take members from this class, skip members from bases.
                using (_processing.Push(m, out var reentered)) {
                    if (reentered) {
                        continue;
                    }
                    switch (m) {
                        case IPythonFunctionType ft1 when ft1.IsLambda():
                            break;
                        case IPythonFunctionType ft2:
                            functions.Add(new FunctionModel(ft2));
                            break;
                        case IPythonClassType cls:
                            classes.Add(new ClassModel(cls));
                            break;
                    }
                }
            }

            Id = callable.Name.GetStableHash();
            Name = callable.Name;
            QualifiedName = callable.QualifiedName;
            Documentation = callable.Documentation;
            Classes = classes.ToArray();
            Functions = functions.ToArray();
            IndexSpan = callable.Location.IndexSpan.ToModel();

            Attributes = FunctionAttributes.Normal;
            if (callable.IsAbstract) {
                Attributes |= FunctionAttributes.Abstract;
            }
            if(callable is IPythonFunctionType ft) {
                if(ft.IsClassMethod) {
                    Attributes |= FunctionAttributes.ClassMethod;
                }
                if (ft.IsStatic) {
                    Attributes |= FunctionAttributes.Static;
                }
            }
        }

        protected override IEnumerable<MemberModel> GetMemberModels() => Classes.Concat<MemberModel>(Functions);
    }
}
