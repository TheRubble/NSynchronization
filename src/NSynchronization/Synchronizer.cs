using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NSynchronization
{
    public sealed class Synchronizer<T> : IDisposable
    {
        private readonly object lockObject = new object();
        
        private readonly 
            ConcurrentDictionary<string, ConcurrentBag<(CancellationTokenRegistration tokenRegistration, TaskCompletionSource<T> completionSource)>> registeredWorkItems
            = new ConcurrentDictionary<string, ConcurrentBag<(CancellationTokenRegistration, TaskCompletionSource<T>)>>();

        public Task<T> ExecuteOrJoinCurrentRunning(string identifier, Func<Task<T>> workload, CancellationToken cancellationToken = default)
        {
            var completionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            /* Register to the calling token so the task completion source token can be cancelled DISPOSE ME */
            var registration = cancellationToken.Register(() =>
            {
                completionSource.TrySetCanceled(cancellationToken);
            });
            
            lock (this.lockObject)
            {
                if (this.registeredWorkItems.TryAdd(identifier, new ConcurrentBag<(CancellationTokenRegistration, TaskCompletionSource<T>)>()))
                {
                    this.registeredWorkItems[identifier].Add((registration, completionSource));
                    this.RunWorkloadOnThreadPool(identifier, workload);
                }
                else
                {
                    this.registeredWorkItems[identifier].Add((registration, completionSource));
                }
                return completionSource.Task;
            }
        }

        private void RunWorkloadOnThreadPool(string identifier, Func<Task<T>> workload)
        {
            Task.Run(async () =>
            {
                Exception ex = default;
                T workloadResult = default;

                try
                {
                    workloadResult = await workload();
                }
                catch (Exception e)
                {
                    ex = e;
                }

                ConcurrentBag<(CancellationTokenRegistration tr, TaskCompletionSource<T> ts)> observingConsumers;
                lock (this.lockObject)
                {
                    this.registeredWorkItems.TryRemove(identifier, out observingConsumers);
                }

                /* Need to head down another path here */
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    Console.WriteLine("I'm a stream");
                }

                foreach (var consumer in observingConsumers)
                {
                    if (ex != null)
                    {
                        consumer.ts.TrySetException(ex.Copy());
                    }
                    else
                    {
                        consumer.tr.Dispose();
                        consumer.ts.TrySetResult(workloadResult.Copy());
                    }
                }
            });
        }

        public void Dispose()
        {
            lock (this.lockObject)
            {
                foreach (var workItems in this.registeredWorkItems)
                {
                    foreach (var observer in workItems.Value)
                    {
                        observer.tokenRegistration.Dispose();
                        observer.completionSource.TrySetCanceled();
                    }
                }
            }
            
            GC.SuppressFinalize(this);           
        }
    }
}