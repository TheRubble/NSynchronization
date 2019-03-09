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
        
        private readonly ConcurrentDictionary<string, ConcurrentBag<TaskCompletionSource<T>>> registeredWorkItems
            = new ConcurrentDictionary<string, ConcurrentBag<TaskCompletionSource<T>>>();

        public Task<T> ExecuteOrJoinCurrentRunning(string identifier, Func<Task<T>> workload, CancellationToken cancellationToken = default)
        {
            var completionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            /* Register to the calling token so the task completion source token can be cancelled DISPOSE ME */
            cancellationToken.Register(() =>
            {
                completionSource.TrySetCanceled(cancellationToken);
            });
            
            lock (this.lockObject)
            {
                if (this.registeredWorkItems.TryAdd(identifier, new ConcurrentBag<TaskCompletionSource<T>>()))
                {
                    this.registeredWorkItems[identifier].Add(completionSource);
                    this.RunWorkloadOnThreadPool(identifier, workload);
                }
                else
                {
                    this.registeredWorkItems[identifier].Add(completionSource);
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

                ConcurrentBag<TaskCompletionSource<T>> observingConsumers;
                lock (this.lockObject)
                {
                    this.registeredWorkItems.TryRemove(identifier, out observingConsumers);
                }

                /* Need to head down another path here */
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    Console.WriteLine("I'm a stream");
                }

                foreach (var taskCompletion in observingConsumers)
                {
                    if (ex != null)
                    {
                        taskCompletion.TrySetException(ex.Copy());
                    }
                    else
                    {
                        taskCompletion.TrySetResult(workloadResult.Copy());
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
                        observer.TrySetCanceled();
                    }
                }
            }
            
            GC.SuppressFinalize(this);           
        }
    }
}