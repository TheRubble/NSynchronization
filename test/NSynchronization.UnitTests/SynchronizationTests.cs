using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NSynchronization.UnitTests
{
    public class SynchronizationTests
    {
        private int called;
        private readonly ManualResetEvent resetEvent = new ManualResetEvent(false);

        [Fact]
        public async Task Should_Execute_Payload_Returning_TaskOfInt()
        {
            // Arrange
            using (var subject = new Synchronizer<int>())
            {
                // Act
                var result = await subject.ExecuteOrJoinCurrentRunning("payload", () => Task.FromResult(1));

                // Assert
                Assert.Equal(1, result);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task Should_Execute_Same_Workload_Once_With_Multiple_Invocations(int counter)
        {
            // Arrange
            using (var subject = new Synchronizer<int>())
            {
                var taskList = new List<Task<int>>(counter);

                // Act
                for (var i = 0; i < counter; i++)
                {
                    taskList.Add(subject.ExecuteOrJoinCurrentRunning("sameId", async () => await FakeDelay()));
                }

                resetEvent.Set();

                await Task.WhenAll(taskList);

                // Assert
                foreach (var task in taskList)
                {
                    Assert.Equal(1, await task);
                }

                Assert.Equal(1, this.called);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task Should_Execute_Once_Per_Invocation_For_Differing_Workload(int counter)
        {
            // Arrange
            using (var subject = new Synchronizer<int>())
            {
                var taskList = new List<Task<int>>(counter);

                // Act
                for (var i = 0; i < counter; i++)
                {
                    taskList.Add(subject.ExecuteOrJoinCurrentRunning(Guid.NewGuid().ToString(),
                        async () => await FakeDelay()));
                }

                resetEvent.Set();

                await Task.WhenAll(taskList);

                // Assert
                foreach (var task in taskList)
                {
                    Assert.Equal(1, await task);
                }

                Assert.Equal(counter, this.called);
            }
        }


        private Task<int> FakeDelay()
        {
            Interlocked.Increment(ref this.called);
            resetEvent.WaitOne(TimeSpan.FromSeconds(30));
            return Task.FromResult(1);
        }
    }
}