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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Python.UnitTests.Core.MSTest {
    [AttributeUsage(AttributeTargets.Method)]
    public class MatrixTestMethodAttribute : DataTestMethodAttribute, ITestDataSource {
        private readonly Dictionary<object[], string> _names = new Dictionary<object[], string>();

        public string NameFormat { get; set; }

        public MatrixTestMethodAttribute() {
            NameFormat = "{0} ({1}) x ({2})";
        }

        public IEnumerable<object[]> GetData(MethodInfo methodInfo) {
            var rows = methodInfo.GetCustomAttributes<MatrixRowAttribute>();
            var columns = methodInfo.GetCustomAttributes<MatrixColumnAttribute>().ToArray();
            foreach (var row in rows) {
                foreach (var column in columns) {
                    var data = new object[row.Data.Length + column.Data.Length];
                    Array.Copy(row.Data, 0, data, 0, row.Data.Length);
                    Array.Copy(column.Data, 0, data, row.Data.Length, column.Data.Length);

                    var rowName = row.DisplayName ?? string.Join(", ", row.Data);
                    var columnName = column.DisplayName ?? string.Join(", ", column.Data);
                    var name = string.Format(CultureInfo.CurrentCulture, NameFormat, methodInfo.Name, rowName, columnName);
                    _names[data] = name;

                    yield return data;
                }
            }
        }

        public string GetDisplayName(MethodInfo methodInfo, object[] data) {
            return _names.TryGetValue(data, out var name) ? name : methodInfo.Name + new Random().Next();
        }
    }
}