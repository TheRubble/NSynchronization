using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace NSynchronization.UnitTests
{
    public class SynchronizationDisposableTests
    {
        private int called;
        private readonly ManualResetEvent resetEvent = new ManualResetEvent(false);
        
        [Theory]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10000)]
        public void Should_Call_All_Observers_With_Task_Cancellation_On_Disposal(int counter)
        {
            // Arrange
            var subject = new Synchronizer<int>();
            var taskList = new List<Task<int>>(counter);
           
            // Act
            for (var i = 0; i < counter; i++)
            {
                taskList.Add(subject.ExecuteOrJoinCurrentRunning("sameId",async () => await FakeDelay()));
            }

            subject.Dispose();
            
            // Assert
            foreach (var task in taskList)
            {
                task.IsCanceled.Should().BeTrue();
            }

            /* On larger runs you'll most likely into the function invocation, due to all having the same
             workload id this should be 0 or 1 at most. */
            this.called.Should().BeLessThan(2);
        }
        
       
        private Task<int> FakeDelay()
        {
            Interlocked.Increment(ref this.called);
            resetEvent.WaitOne(TimeSpan.FromSeconds(30));
            return Task.FromResult(1);
        }
    }
}