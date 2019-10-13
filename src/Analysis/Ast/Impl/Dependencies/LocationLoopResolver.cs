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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Dependencies {
    internal static class LocationLoopResolver<T> {
        public static ImmutableArray<T> FindStartingItems(IEnumerable<(T From, int FromLocation, T To, int ToLocation)> edges) {
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

            var startingIndices = FindStartingIndices(groupedEdges);
            return startingIndices.Select(i => itemToIndex.First(j => j.Value == i).Key).ToImmutableArray();
            
            int SortByFromLocation((int FromLocation, int, int) x, (int FromLocation, int, int) y) => x.FromLocation.CompareTo(y.FromLocation);
        }

        private static IEnumerable<int> FindStartingIndices(List<List<(int FromLocation, int ToIndex, int ToLocation)>> groupedEdges) {
            var walkedIndices = new int[groupedEdges.Count];
            var visited = new bool[groupedEdges.Count];
            var path = new Stack<int>();
            var startingIndex = 0;
            var allVisitedBeforeIndex = 0;

            while (startingIndex < groupedEdges.Count) {
                if (visited[startingIndex]) {
                    if (startingIndex == allVisitedBeforeIndex) {
                        allVisitedBeforeIndex++;
                    }

                    startingIndex++;
                    continue;
                }

                for (var i = 0; i < walkedIndices.Length; i++) {
                    walkedIndices[i] = -1;
                }

                path.Clear();

                if (!IsWalkable(groupedEdges, startingIndex, walkedIndices, visited, path)) {
                    startingIndex++;
                    continue;
                }

                for (var i = 0; i < walkedIndices.Length; i++) {
                    if (walkedIndices[i] != -1) {
                        visited[i] = true;
                    }
                }

                yield return startingIndex;
                startingIndex = allVisitedBeforeIndex;
            }
        }

        private static bool IsWalkable(in List<List<(int FromLocation, int ToIndex, int ToLocation)>> groupedEdges, in int startGroupIndex, in int[] walkedIndices, in bool[] visited, in Stack<int> path) {
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
                if (visited[toGroupIndex]) {
                    continue;
                }

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
