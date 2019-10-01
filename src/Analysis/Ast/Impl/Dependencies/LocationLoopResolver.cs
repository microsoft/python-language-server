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

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Python.Analysis.Dependencies {
    internal static class LocationLoopResolver<T> {
        public static T FindStartingItem(IEnumerable<(T From, int FromLocation, T To, int ToLocation)> edges) {
            var itemToIndex = new Dictionary<T, int>();
            var groupedEdges = new List<List<(int FromLocation, int ToIndex, int ToLocation)>>();
            var index = 0;
            foreach (var (fromItem, fromLocation, toItem, toLocation) in edges) {
                if (!itemToIndex.TryGetValue(fromItem, out var fromIndex)) {
                    fromIndex = index++;
                    groupedEdges.Add(new List<(int, int, int)>());
                    itemToIndex[fromItem] = fromIndex;
                }
                
                if (!itemToIndex.TryGetValue(toItem, out var toIndex)) {
                    toIndex = index++;
                    groupedEdges.Add(new List<(int, int, int)>());
                    itemToIndex[toItem] = toIndex;
                }
                
                groupedEdges[fromIndex].Add((fromLocation, toIndex, toLocation));
            }

            foreach (var group in groupedEdges) {
                group.Sort(SortByFromLocation);
            }

            var startingIndex = FindStartingIndices(groupedEdges);
            return startingIndex == -1 ? default : itemToIndex.First(i => i.Value == startingIndex).Key;
            
            int SortByFromLocation((int FromLocation, int, int) x, (int FromLocation, int, int) y) => x.FromLocation.CompareTo(y.FromLocation);
        }

        private static int FindStartingIndices(in List<List<(int FromLocation, int ToIndex, int ToLocation)>> groupedEdges) {
            var walkedIndices = new int[groupedEdges.Count];
            var path = new Stack<int>();

            for (var startingIndex = 0; startingIndex < groupedEdges.Count; startingIndex++) {
                for (var i = 0; i < walkedIndices.Length; i++) {
                    walkedIndices[i] = -1;
                }
                path.Clear();
                
                if (IsWalkable(groupedEdges, startingIndex, walkedIndices, path)) {
                    return startingIndex;
                }
            }

            return -1;
        }

        private static bool IsWalkable(in List<List<(int FromLocation, int ToIndex, int ToLocation)>> groupedEdges, in int startGroupIndex, in int[] walkedIndices, in Stack<int> path) {
            const int notVisited = -1;
            var fromGroupIndex = startGroupIndex;
            
            while (true) {
                var indexInFromGroup = ++walkedIndices[fromGroupIndex];
                var fromGroup = groupedEdges[fromGroupIndex];
                if (fromGroup.Count == indexInFromGroup) {
                    if (path.Count == 0) {
                        return true;
                    }

                    fromGroupIndex = path.Pop();
                    continue;
                }

                var edge = fromGroup[indexInFromGroup];
                var toGroupIndex = edge.ToIndex;
                var indexInToGroup = walkedIndices[toGroupIndex];
                if (indexInToGroup == notVisited) {
                    path.Push(fromGroupIndex);
                    fromGroupIndex = toGroupIndex;
                    continue;
                }
                
                var toGroup = groupedEdges[toGroupIndex];
                if (toGroup.Count == indexInToGroup) {
                    continue;
                }

                var requiredPosition = edge.ToLocation;
                var currentPosition = toGroup[indexInToGroup].FromLocation;
                if (requiredPosition > currentPosition) {
                    return false;
                }
            }
        }
    }
}
