using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace NSynchronization.Benchmark.Tests
{
    [MemoryDiagnoser]
    public class MemoryPressure
    {
        private async Task<int> FakeDelay()
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            return 1;
        }

        [Benchmark(OperationsPerInvoke = 1)]
        public async Task Workload()
        {
            var loopCount = 100000;
            var taskList = new List<Task<int>>(loopCount);
            var cancellationTokenSource = new CancellationTokenSource();
            using (var subject = new Synchronizer<int>())
            {
                for (int i = 0; i < loopCount; i++)
                {
                    taskList.Add(subject.ExecuteOrJoinCurrentRunning("payload", async () => await FakeDelay(), cancellationTokenSource.Token));
                }

                await Task.WhenAll(taskList);
            }
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MemoryPressure>();
        }
    }
}