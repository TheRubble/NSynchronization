using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace NSynchronization.UnitTests
{
    public class SynchronizationCancellationTests
    {
        private int called;
        private ManualResetEvent resetEvent = new ManualResetEvent(false);
        
        [Fact]
        public void Should_Cancel_Workload()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            using (var subject = new Synchronizer<int>())
            {
                // Act
                Func<Task<int>> firstResult = () => subject.ExecuteOrJoinCurrentRunning("payload", async () => await FakeDelay(), cancellationTokenSource.Token);
                cancellationTokenSource.Cancel();
                
                // Assert
                firstResult.Should().Throw<TaskCanceledException>();
            }
        }

        [Fact]
        public async Task Should_Cancel_Only_One_Allowing_Others_To_Complete()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            var completeExecutionCancellationTokenSource = new CancellationTokenSource();
            
            using (var subject = new Synchronizer<int>())
            {
                // Act
                var shouldGetCancelledTask =  subject.ExecuteOrJoinCurrentRunning("payload", async () => await FakeDelay(), cancellationTokenSource.Token);
                var runToCompletionTask = subject.ExecuteOrJoinCurrentRunning("payload", async () => await FakeDelay(), completeExecutionCancellationTokenSource.Token);
                cancellationTokenSource.Cancel();
                resetEvent.Set();
                
                var completionResult = await runToCompletionTask;
                
                // Assert
                Assert.True(shouldGetCancelledTask.IsCanceled);
                completionResult.Should().Be(1);
                this.called.Should().Be(1);
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