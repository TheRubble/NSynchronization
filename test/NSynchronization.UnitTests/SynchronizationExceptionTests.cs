using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace NSynchronization.UnitTests
{
    public class SynchronizationExceptionTests
    {
        private int called;
        private readonly ManualResetEvent resetEvent = new ManualResetEvent(false);

        [Fact]
        public void Should_Throw_Exception_If_Workload_Throws_Exception()
        {
            // Arrange
            var subject = new Synchronizer<int>();

            // Act
            Func<Task<int>> result = async () =>
                await subject.ExecuteOrJoinCurrentRunning(Guid.NewGuid().ToString(), async () => await FakeDelay());
            resetEvent.Set();

            // Assert
            result.Should().Throw<Exception>().WithMessage("faked exception");
        }

        [Theory]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task Should_Throw_Same_Exception_For_Identical_Workload_With_Multiple_Invocations(int counter)
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

                // Assert
                foreach (var task in taskList)
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception ex)
                    {
                        Assert.Equal("faked exception", ex.Message);
                    }
                }

                Assert.Equal(1, this.called);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10000)]
        public async Task Should_Throw_Unique_Exception_For_Differing_Workload_With_Multiple_Invocations(int counter)
        {
            // Arrange
            using (var subject = new Synchronizer<int>())
            {
                var taskList = new List<(int loopCount, Task<int> workload)>(counter);

                // Act
                for (var i = 0; i < counter; i++)
                {
                    var capturedLoopCount = i;
                    taskList.Add((capturedLoopCount,
                        subject.ExecuteOrJoinCurrentRunning(Guid.NewGuid().ToString(),
                            async () => await FakeDelayWithContext(capturedLoopCount))));
                }

                resetEvent.Set();

                // Assert
                foreach (var task in taskList)
                {
                    try
                    {
                        await task.workload;
                    }
                    catch (Exception ex)
                    {
                        Assert.Equal($"faked exception {task.loopCount}", ex.Message);
                    }
                }

                Assert.Equal(counter, this.called);
            }
        }

        private Task<int> FakeDelay()
        {
            Interlocked.Increment(ref this.called);
            this.resetEvent.WaitOne();
            throw new Exception("faked exception");
        }

        private Task<int> FakeDelayWithContext(int id)
        {
            Interlocked.Increment(ref this.called);
            this.resetEvent.WaitOne();
            throw new Exception($"faked exception {id}");
        }
    }
}