using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NSynchronization.UnitTests
{
    public class SynchronizationResponseCopyTests
    {
        private int called;
        private ManualResetEvent resetEvent = new ManualResetEvent(false);
        
        [Fact]
        public async Task Should_Return_Different_Instances_Of_Ref_Types_For_The_Same_Workload()
        {
            // Arrange
            using (var subject = new Synchronizer<SampleObject>())
            {
                // Act
                var firstResult = subject.ExecuteOrJoinCurrentRunning("payload", async () => await FakeDelay());
                var secondResult = subject.ExecuteOrJoinCurrentRunning("payload", async () => await FakeDelay());
                
                resetEvent.Set();
                await Task.WhenAll(firstResult, secondResult);

                // Assert
                Assert.NotEqual(await firstResult, await secondResult);
                Assert.Equal(1, this.called);
            }
        }

        private class SampleObject
        {
            public string FirstName { get; set; }
            public string SecondName { get; set; }
            public int Age { get; set; }
            public DateTime DateOfRegistration { get; set; }
        }
        
        private Task<SampleObject> FakeDelay()
        {
            Interlocked.Increment(ref this.called);
            resetEvent.WaitOne(TimeSpan.FromSeconds(30));
            return Task.FromResult(new SampleObject()
            {
                FirstName = "Buck",
                SecondName = "Rogers",
                Age = 65,
                DateOfRegistration = DateTime.Now.Subtract(TimeSpan.FromDays(500))
            });
        }
    }
}