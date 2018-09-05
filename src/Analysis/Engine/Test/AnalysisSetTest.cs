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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.AnalysisSetDetails;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    class TestAnalysisValue : AnalysisValue {
        public string _name;

        public override string Name {
            get { return _name; }
        }

        public string Value;
        public int MergeCount;

        public TestAnalysisValue() {
            MergeCount = 1;
        }

        public override bool Equals(object obj) {
            var tns = obj as TestAnalysisValue;
            if (tns == null) {
                return false;
            }

            return Name.Equals(tns.Name) && Value.Equals(tns.Value) && MergeCount.Equals(tns.MergeCount);
        }

        public override int GetHashCode() {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            var tns = ns as TestAnalysisValue;
            if (tns == null) {
                return false;
            }

            return Name.Equals(tns.Name);
        }

        internal override int UnionHashCode(int strength) {
            return Name.GetHashCode();
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            var tns = ns as TestAnalysisValue;
            if (tns == null || object.ReferenceEquals(this, tns)) {
                return this;
            }

            return new TestAnalysisValue {
                _name = Name,
                Value = MergeCount > tns.MergeCount ? Value : tns.Value,
                MergeCount = MergeCount + tns.MergeCount
            };
        }

        public override string ToString() {
            return string.Format("{0}:{1}", Name, Value);
        }
    }

    class SubTestAnalysisValue : TestAnalysisValue { }

    [TestClass]
    public class AnalysisSetTest {
        private static readonly AnalysisValue nsA1 = new TestAnalysisValue {_name = "A", Value = "a"};
        private static readonly AnalysisValue nsA2 = new TestAnalysisValue {_name = "A", Value = "x"};
        private static readonly AnalysisValue nsAU1 = nsA1.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU2 = nsAU1.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU3 = nsAU2.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU4 = nsAU3.UnionMergeTypes(nsA2, 100);

        private static readonly AnalysisValue nsB1 = new TestAnalysisValue {_name = "B", Value = "b"};
        private static readonly AnalysisValue nsB2 = new TestAnalysisValue {_name = "B", Value = "y"};
        private static readonly AnalysisValue nsBU1 = nsB1.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU2 = nsBU1.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU3 = nsBU2.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU4 = nsBU3.UnionMergeTypes(nsB2, 100);

        private static readonly AnalysisValue nsC1 = new TestAnalysisValue {_name = "C", Value = "c"};
        private static readonly AnalysisValue nsC2 = new TestAnalysisValue {_name = "C", Value = "z"};
        private static readonly AnalysisValue nsCU1 = nsC1.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU2 = nsCU1.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU3 = nsCU2.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU4 = nsCU3.UnionMergeTypes(nsC2, 100);

        [TestMethod, Priority(0)]
        public void SetOfOne_Object() {
            var set = AnalysisSet.Create(nsA1);
            Assert.AreSame(nsA1, set);

            set = AnalysisSet.Create(new[] {nsA1}.AsEnumerable());
            Assert.AreSame(nsA1, set);

            set = AnalysisSet.Create(new[] {nsA1, nsA1}.AsEnumerable());
            Assert.AreSame(nsA1, set);

            set = AnalysisSet.Create(new[] {nsA1, nsA2}.AsEnumerable());
            Assert.AreNotSame(nsA1, set);
        }

        [TestMethod, Priority(0)]
        public void SetOfOne_Union() {
            var set = AnalysisSet.CreateUnion(nsA1, UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisSetOneUnion>().And.OnlyContain(nsA1);

            set = AnalysisSet.CreateUnion(new[] {nsA1}.AsEnumerable(), UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisSetOneUnion>().And.OnlyContain(nsA1);

            set = AnalysisSet.CreateUnion(new[] {nsA1, nsA1}.AsEnumerable(), UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisSetOneUnion>().And.OnlyContain(nsA1);

            set = AnalysisSet.CreateUnion(new[] {nsA1, nsA2}.AsEnumerable(), UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisSetOneUnion>().And.OnlyContain(nsAU1);
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Object() {
            var set = AnalysisSet.Create(new[] {nsA1, nsA2});
            set.Should().BeOfType<AnalysisSetTwoObject>().And.OnlyContain(nsA1, nsA2);

            set = AnalysisSet.Create(new[] {nsA1, nsA1, nsA2, nsA2});
            set.Should().BeOfType<AnalysisSetTwoObject>().And.OnlyContain(nsA1, nsA2);
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Union() {
            var set = AnalysisSet.CreateUnion(new[] {nsA1, nsA2, nsB1, nsB2}, UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisSetTwoUnion>().And.OnlyContain(nsAU1, nsBU1);

            set = AnalysisSet.CreateUnion(new[] {nsA1, nsA1, nsA2, nsA2, nsB1, nsB1, nsB2, nsB2},
                UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisSetTwoUnion>().And.OnlyContain(nsAU2, nsBU2);
        }

        [TestMethod, Priority(0)]
        public void ManySet_Object() {
            var set = AnalysisSet.Create(new[] {nsA1, nsA2, nsB1, nsB2});
            set.Should().BeOfType<AnalysisHashSet>().And.OnlyContain(nsA1, nsA2, nsB1, nsB2);

            set = AnalysisSet.Create(new[] {nsA1, nsA1, nsA2, nsA2, nsB1, nsB1, nsB2, nsB2});
            set.Should().BeOfType<AnalysisHashSet>().And.OnlyContain(nsA1, nsA2, nsB1, nsB2);
        }

        [TestMethod, Priority(0)]
        public void ManySet_Union() {
            var set = AnalysisSet.CreateUnion(new[] {nsA1, nsA2, nsB1, nsB2, nsC1}, UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisHashSet>().And.OnlyContain(nsAU1, nsBU1, nsC1);

            set = AnalysisSet.CreateUnion(new[] {nsA1, nsA1, nsA2, nsA2, nsB1, nsB1, nsB2, nsB2, nsC1},
                UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisHashSet>().And.OnlyContain(nsAU2, nsBU2, nsC1);
        }

        [TestMethod, Priority(0)]
        public void EmptySet_Add_Object() {
            var set = AnalysisSet.Empty;
            set.Should().BeOfType<AnalysisSetEmptyObject>();

            set = AnalysisSet.Create();
            set.Should().BeOfType<AnalysisSetEmptyObject>().And.BeSameAs(AnalysisSet.Empty);

            set = set.Add(nsA1, out var added, false);
            added.Should().BeTrue();
            set.Should().BeSameAs(nsA1);

            set = AnalysisSet.Empty;
            set = set.Add(nsA1, out added, true);
            added.Should().BeTrue();
            set.Should().BeSameAs(nsA1);
        }

        [TestMethod, Priority(0)]
        public void EmptySet_Add_Union() {
            var set = AnalysisSet.EmptyUnion;
            set.Should().BeOfType<AnalysisSetEmptyUnion>();

            set = AnalysisSet.CreateUnion(UnionComparer.Instances[0]);
            set.Should().BeOfType<AnalysisSetEmptyUnion>().And.BeSameAs(AnalysisSet.EmptyUnion);

            set = set.Add(nsA1, out var added, false);
            added.Should().BeTrue();
            set.Should().OnlyContain(nsA1);

            set = AnalysisSet.EmptyUnion;
            set = set.Add(nsA1, out added, true);
            added.Should().BeTrue();
            set.Should().OnlyContain(nsA1);
        }

        [TestMethod, Priority(0)]
        public void SetOfOne_Add_Object() {
            var set = AnalysisSet.Create(nsA1);

            set = set.Add(nsA1, out var added, true);
            added.Should().BeFalse();
            set.Should().BeSameAs(nsA1);

            set = set.Add(nsA1, out added, false);
            added.Should().BeFalse();
            set.Should().BeSameAs(nsA1);

            set = set.Add(nsB1, out added, true);
            added.Should().BeTrue();
            set.Should().BeOfType<AnalysisSetTwoObject>();

            set = AnalysisSet.Create(nsA1);
            var set2 = set.Add(nsA1, out added, true);
            added.Should().BeFalse();
            set2.Should().BeSameAs(set).And.OnlyContain(nsA1);
        }

        [TestMethod, Priority(0)]
        public void SetOfOne_Add_Union() {
            var set = AnalysisSet.CreateUnion(nsA1, UnionComparer.Instances[0]);

            set = set.Add(nsA1, out var added, true);
            added.Should().BeFalse();
            set.Should().BeOfType<AnalysisSetOneUnion>();

            set = set.Add(nsA1, out added, false);
            added.Should().BeFalse();
            set.Should().BeOfType<AnalysisSetOneUnion>();

            set = set.Add(nsB1, out added, true);
            added.Should().BeTrue();
            set.Should().BeOfType<AnalysisSetTwoUnion>().And.OnlyContain(nsA1, nsB1);

            set = AnalysisSet.CreateUnion(nsA1, UnionComparer.Instances[0]);
            var set2 = set.Add(nsA2, out added, true);
            added.Should().BeTrue();
            set2.Should().NotBeSameAs(set).And.OnlyContain(nsAU1);
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Add_Object() {
            var set = AnalysisSet.Create(new[] {nsA1, nsB1});
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] {nsA1, nsB1}) {
                set2 = set.Add(o, out added, true);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();

                set2 = set.Add(o, out added, false);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();
            }

            foreach (var o in new[] {nsA2, nsB2, nsC1, nsC2}) {
                set2 = set.Add(o, out added, true);
                set2.Should().NotBeSameAs(set).And.OnlyContain(nsA1, nsB1, o);
                added.Should().BeTrue();

                set2 = set.Add(o, out added, false);
                set2.Should().NotBeSameAs(set).And.OnlyContain(nsA1, nsB1, o);
                added.Should().BeTrue();
            }
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Add_Union() {
            var set = AnalysisSet.CreateUnion(new[] {nsA1, nsB1}, UnionComparer.Instances[0]);
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] {nsA1, nsB1}) {
                set2 = set.Add(o, out added, true);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();

                set2 = set.Add(o, out added, false);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();
            }

            foreach (var o in new[] {nsC1, nsC2}) {
                set2 = set.Add(o, out added, true);
                set2.Should().NotBeSameAs(set)
                    .And.OnlyContain(nsA1, nsB1, o);
                added.Should().BeTrue();

                set2 = set.Add(o, out added, false);
                set2.Should().NotBeSameAs(set)
                    .And.OnlyContain(nsA1, nsB1, o);
                added.Should().BeTrue();
            }

            set2 = set.Add(nsA2, out added, true);
            set2.Should().NotBeSameAs(set)
                .And.BeOfType<AnalysisSetTwoUnion>()
                .And.OnlyContain(nsAU1, nsB1);
            added.Should().BeTrue();

            set2 = set.Add(nsB2, out added, false);
            set2.Should().NotBeSameAs(set)
                .And.BeOfType<AnalysisSetTwoUnion>()
                .And.OnlyContain(nsA1, nsBU1);
            added.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void ManySet_Add_Object() {
            var set = AnalysisSet.Create(new[] {nsA1, nsB1, nsC1});
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] {nsA1, nsB1, nsC1}) {
                set2 = set.Add(o, out added, true);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();

                set2 = set.Add(o, out added, false);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();
            }

            foreach (var o in new[] {nsA2, nsB2, nsC2}) {
                set = AnalysisSet.Create(new[] {nsA1, nsB1, nsC1});
                set2 = set.Add(o, out added, false);
                set2.Should().NotBeSameAs(set)
                    .And.OnlyContain(nsA1, nsB1, nsC1, o);
                added.Should().BeTrue();

                set2 = set.Add(o, out added, true);
                set2.Should().BeSameAs(set)
                    .And.OnlyContain(nsA1, nsB1, nsC1, o);
                added.Should().BeTrue();
            }
        }

        [TestMethod, Priority(0)]
        public void ManySet_Add_Union() {
            var set = AnalysisSet.CreateUnion(new[] {nsA1, nsB1, nsC1}, UnionComparer.Instances[0]);
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] {nsA1, nsB1, nsC1}) {
                set2 = set.Add(o, out added, true);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();

                set2 = set.Add(o, out added, false);
                set2.Should().BeSameAs(set);
                added.Should().BeFalse();
            }

            set2 = set.Add(nsA2, out added, true);
            set2.Should().BeSameAs(set)
                .And.BeOfType<AnalysisHashSet>()
                .And.OnlyContain(nsAU1, nsB1, nsC1);
            added.Should().BeTrue();

            set = AnalysisSet.CreateUnion(new[] {nsA1, nsB1, nsC1}, UnionComparer.Instances[0]);
            set2 = set.Add(nsA2, out added, false);
            set2.Should().NotBeSameAs(set)
                .And.BeOfType<AnalysisHashSet>()
                .And.OnlyContain(nsAU1, nsB1, nsC1);
            added.Should().BeTrue();

            set2 = set.Add(nsB2, out added, false);
            set2.Should().NotBeSameAs(set)
                .And.BeOfType<AnalysisHashSet>()
                .And.OnlyContain(nsA1, nsBU1, nsC1);
            added.Should().BeTrue();

            set2 = set.Add(nsC2, out added, false);
            set2.Should().NotBeSameAs(set)
                .And.BeOfType<AnalysisHashSet>()
                .And.OnlyContain(nsA1, nsB1, nsCU1);
            added.Should().BeTrue();
        }

        [TestMethod, Priority(0)]
        public void Set_PredicateSplit() {
            nsA1.Split(v => v.Name == "A", out var trueSet, out var falseSet).Should().BeTrue();
            trueSet.Should().BeSameAs(nsA1);
            falseSet.Should().BeEmpty();

            nsA1.Split(v => v.Name != "A", out trueSet, out falseSet).Should().BeFalse();
            trueSet.Should().BeEmpty();
            falseSet.Should().BeSameAs(nsA1);

            foreach (var cmp in new IEqualityComparer<AnalysisValue>[]
                {ObjectComparer.Instance, UnionComparer.Instances[0]}) {
                var set = AnalysisSet.Create(new[] {nsA1, nsB1, nsC1}, cmp);

                set.Split(v => v.Name == "A", out trueSet, out falseSet).Should().BeTrue();
                trueSet.Should().ContainSingle();
                falseSet.Should().HaveCount(set.Count - 1);
                trueSet.Comparer.Should().BeSameAs(set.Comparer);
                falseSet.Comparer.Should().BeSameAs(set.Comparer);
                
                set.Split(v => v.Name == "X", out trueSet, out falseSet).Should().BeFalse();
                trueSet.Should().BeEmpty();
                falseSet.Should().HaveCount(set.Count);
                trueSet.Comparer.Should().BeSameAs(set.Comparer);
                falseSet.Comparer.Should().BeSameAs(set.Comparer);

                set.Split(v => v.Name != null, out trueSet, out falseSet).Should().BeTrue();
                trueSet.Should().HaveCount(set.Count);
                falseSet.Should().BeEmpty();
                trueSet.Comparer.Should().BeSameAs(set.Comparer);
                falseSet.Comparer.Should().BeSameAs(set.Comparer);
            }
        }

        [TestMethod, Priority(0)]
        public void Set_TypeSplit() {
            var testAv = new TestAnalysisValue {_name = "A", Value = "A"};
            var subTestAv = new SubTestAnalysisValue {_name = "B", Value = "B"};

            subTestAv.Split(out IReadOnlyList<SubTestAnalysisValue> ofType, out var rest).Should().BeTrue();
            ofType.Should().ContainSingle().Which.Should().BeSameAs(subTestAv);
            rest.Should().BeEmpty();

            testAv.Split(out ofType, out rest).Should().BeFalse();
            ofType.Should().BeEmpty();
            rest.Should().BeSameAs(testAv);

            var set = AnalysisSet.Create(new[] {testAv, subTestAv});
            set.Split(out ofType, out rest).Should().BeTrue();
            ofType.Should().ContainSingle();
            rest.Should().BeSameAs(testAv);

            set = AnalysisSet.Create(new[] {nsA1, nsB1, nsC1});
            set.Split(out ofType, out rest).Should().BeFalse();
            ofType.Should().BeEmpty();
            rest.Should().BeSameAs(set);
        }
    }
}