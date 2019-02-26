using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Python.UnitTests.Core.FluentAssertions;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class DependencyResolverTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

// ReSharper disable StringLiteralTypo
        [DataRow("A:BC|B:C|C", "CBA")]
        [DataRow("C|A:BC|B:C", "CBA")]
        [DataRow("C|B:AC|A:BC", "CBABA")]
        [DataRow("A:CE|B:A|C:B|D:B|E", "[BE]CAB[DC]A")]
        [DataRow("A:D|B:DA|C:BA|D:AE|E", "[AE]DADBC")]
        [DataRow("A:C|C:B|B:A|D:AF|F:CE|E:BD", "ABCA[DB][EC]FDEF")]
        [DataRow("A:BC|B:AC|C:BA|D:BC", "ACBACBD")]
        [DataRow("A|B|C|D:AB|E:BC", "[ABC][DE]")]
        [DataRow("A:CE|B:A|C:B|D:BC|E|F:C", "[BE]CABC[FDA]")]
// ReSharper restore StringLiteralTypo
        [DataTestMethod]
        public async Task AddChangesAsync(string input, string output) {
            var resolver = new DependencyResolver<string, string>(new AddChangesAsyncTestDependencyFinder());
            var splitInput = input.Split("|");

            var walker = default(IDependencyChainWalker<string, string>);
            foreach (var value in splitInput) {
                walker = await resolver.AddChangesAsync(value.Split(":")[0], value, default);
            }

            var result = new StringBuilder();
            var tasks = new List<Task<IDependencyChainNode<string>>>();
            while (!walker.IsCompleted) {
                var nodeTask = walker.GetNextAsync(default);
                if (!nodeTask.IsCompleted) {
                    if (tasks.Count > 1) {
                        result.Append('[');
                    }

                    foreach (var task in tasks) {
                        result.Append(task.Result.Value[0]);
                        task.Result.MarkCompleted();
                    }

                    if (tasks.Count > 1) {
                        result.Append(']');
                    }

                    tasks.Clear();
                }
                tasks.Add(nodeTask);
            }

            result.ToString().Should().Be(output);
        }

        [TestMethod]
        public void AddChangesAsync_Parallel() {
            var resolver = new DependencyResolver<int, int>(new AddChangesAsyncParallelTestDependencyFinder());
            var tasks = new List<Task<IDependencyChainWalker<int, int>>> {
                resolver.AddChangesAsync(0, 0, default),
                resolver.AddChangesAsync(1, 1, default),
                resolver.AddChangesAsync(0, 0, default),
                resolver.AddChangesAsync(1, 1, default),
                resolver.AddChangesAsync(0, 0, default),
                resolver.AddChangesAsync(1, 1, default)
            };

            tasks[0].Should().BeCanceled();
            tasks[1].Should().BeCanceled();
            tasks[2].Should().BeCanceled();
            tasks[3].Should().BeCanceled();
            tasks[4].Should().BeCanceled();
            tasks[5].Should().NotBeCompleted();
        }

        [TestMethod]
        public async Task AddChangesAsync_RepeatedChange() {
            var resolver = new DependencyResolver<string, string>(new AddChangesAsyncTestDependencyFinder());
            resolver.AddChangesAsync("A", "A:B", default).DoNotWait();
            resolver.AddChangesAsync("B", "B:C", default).DoNotWait();
            var walker = await resolver.AddChangesAsync("C", "C", default);

            var result = new StringBuilder();
            while (!walker.IsCompleted) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.MarkCompleted();
            }

            result.ToString().Should().Be("CBA");

            walker = await resolver.AddChangesAsync("B", "B:C", default);
            result = new StringBuilder();
            while (!walker.IsCompleted) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.MarkCompleted();
            }

            result.ToString().Should().Be("BA");
        }

        [TestMethod]
        public async Task AddChangesAsync_RepeatedChange2() {
            var resolver = new DependencyResolver<string, string>(new AddChangesAsyncTestDependencyFinder());
            resolver.AddChangesAsync("A", "A:B", default).DoNotWait();
            resolver.AddChangesAsync("B", "B", default).DoNotWait();
            resolver.AddChangesAsync("C", "C:D", default).DoNotWait();
            var walker = await resolver.AddChangesAsync("D", "D", default);

            var result = new StringBuilder();
            while (!walker.IsCompleted) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.MarkCompleted();
            }

            result.ToString().Should().Be("BDAC");


            resolver.AddChangesAsync("D", "D", default).DoNotWait();
            walker = await resolver.AddChangesAsync("B", "B:C", default);
            result = new StringBuilder();
            while (!walker.IsCompleted) {
                var node = await walker.GetNextAsync(default);
                result.Append(node.Value[0]);
                node.MarkCompleted();
            }

            result.ToString().Should().Be("DCBA");
        }

        [TestMethod]
        public async Task AddChangesAsync_MissingKeys() {
            var resolver = new DependencyResolver<string, string>(new AddChangesAsyncTestDependencyFinder());
            resolver.AddChangesAsync("A", "A:B", default).DoNotWait();
            resolver.AddChangesAsync("B", "B", default).DoNotWait();
            var walker = await resolver.AddChangesAsync("C", "C:D", default);
            
            var result = new StringBuilder();
            var node = await walker.GetNextAsync(default);
            result.Append(node.Value[0]);
            node.MarkCompleted();
            
            node = await walker.GetNextAsync(default);
            result.Append(node.Value[0]);
            node.MarkCompleted();
            
            walker.MissingKeys.Should().Equal("D");
            result.ToString().Should().Be("BC");

            walker = await resolver.AddChangesAsync("D", "D", default);
            result = new StringBuilder();
            result.Append((await walker.GetNextAsync(default)).Value[0]);
            result.Append((await walker.GetNextAsync(default)).Value[0]);
            
            walker.MissingKeys.Should().BeEmpty();
            result.ToString().Should().Be("AD");
        }

        private sealed class AddChangesAsyncParallelTestDependencyFinder : IDependencyFinder<int, int> {
            public Task<ImmutableArray<int>> FindDependenciesAsync(int value, CancellationToken cancellationToken) 
                => new TaskCompletionSource<ImmutableArray<int>>().Task.ContinueWith(t => t.GetAwaiter().GetResult(), cancellationToken);
        }

        private sealed class AddChangesAsyncTestDependencyFinder : IDependencyFinder<string, string> {
            public Task<ImmutableArray<string>> FindDependenciesAsync(string value, CancellationToken cancellationToken) {
                var kv = value.Split(":");
                var dependencies = kv.Length == 1 ? ImmutableArray<string>.Empty : ImmutableArray<string>.Create(kv[1].Select(c => c.ToString()).ToList());
                return Task.FromResult(dependencies);
            }
        }
    }
}
