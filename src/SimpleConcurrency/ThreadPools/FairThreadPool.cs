/*

    Copyright (c) 2011 Serge Danzanvilliers

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

*/

using System;
using System.Collections.Generic;
using System.Threading;

namespace SimpleConcurrency.ThreadPools
{
    using SimpleConcurrency.Patterns;

    /// <summary>
    /// The "Fair Thread Pool", an instanciable thread pool that allows
    /// "fair scheduling" of enqueued workers. Worker jobs are associated to
    /// tags, which may be viewed as a family marker for jobs. Job scheduling
    /// alternate between the tags in round robin each time a thread has to pick
    /// a worker to run. Inside a given tag jobs are scheduled in fifo order.
    /// 
    /// Workers are simply 'Action' or 'Func<>'.
    /// Action can be waited for if you want or simply forgotten.
    /// Func<> may return any type of value. The returned value is accessed
    /// through a Future pattern.
    /// </summary>
    public sealed class FairThreadPool : IDisposable
    {
        /// <summary>
        /// Build a new FairThreadPool and start the threads.
        /// </summary>
        /// <param name="name">A name for the thread pool.</param>
        /// <param name="maxThreads">Maximum number of threads in the pool.</param>
        public FairThreadPool(string name, int maxThreads)
        {
            long id = Interlocked.Increment(ref _id);
            _name = string.Format("FairThreadPool[{0}]({1})", id, name ?? "");
            if (maxThreads <= 0)
            {
                throw new ArgumentOutOfRangeException("maxThreads", maxThreads, _name + ": maximum number of threads cannot be zero or less.");
            }
            NThreads = maxThreads;
            StartThreads();
        }

        /// <summary>
        /// Build a new FairThreadPool and start the threads.
        /// Number of threads will be set to the number of cores
        /// as returned by Environment.ProcessorCount
        /// </summary>
        /// <param name="name">A name for the thread pool.</param>
        public FairThreadPool(string name)
            : this(name, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// Build a new FairThreadPool and start the threads.
        /// </summary>
        /// <param name="maxThreads">Maximum number of threads in the pool.</param>
        public FairThreadPool(int maxThreads)
            : this("", maxThreads)
        {
        }

        /// <summary>
        /// Build a new FairThreadPool and start the threads.
        /// Number of threads will be set to the number of cores
        /// as returned by Environment.ProcessorCount
        /// </summary>
        public FairThreadPool()
            : this("")
        {
        }

        /// <summary>
        /// The name of the FairThreadPool instance.
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Enqueue a job associated to a given queue tag.
        /// </summary>
        /// <param name="tag">Queue tag associated to the job.</param>
        /// <param name="worker">Job to run.</param>
        public void QueueWorker(int tag, Action worker)
        {
            lock (_condition)
            {
                _actions.Enqueue(tag, worker);
                Monitor.Pulse(_condition);
            }
        }

        /// <summary>
        /// Enqueue a job associated to queue tag 0.
        /// </summary>
        /// <param name="worker">Job to run.</param>
        public void QueueWorker(Action worker)
        {
            QueueWorker(0, worker);
        }

        /// <summary>
        /// Enqueue a job and provide a waitable object to wait for
        /// completion. The job is associated to queue tag 0.
        /// </summary>
        /// <param name="worker">Job to run.</param>
        /// <returns>A waitable object allowing waiting for the job completion.</returns>
        public IWaitable QueueWaitableWorker(Action worker)
        {
            return QueueWaitableWorker(0, worker);
        }

        /// <summary>
        /// Enqueue a job and provide a waitable object to wait for
        /// completion.
        /// </summary>
        /// <param name="tag">Queue tag associated to the job.</param>
        /// <param name="worker">Job to run.</param>
        /// <returns>A waitable object allowing waiting for the job completion.</returns>
        public IWaitable QueueWaitableWorker(int tag, Action worker)
        {
            return QueueWorker(tag, () => { worker(); return true; });
        }

        /// <summary>
        /// Enqueue a job returning a value and provide a Future&lt;&gt; to
        /// retrieve the result. The job is associated to queue tag 0.
        /// </summary>
        /// <typeparam name="TData">The type of the result.</typeparam>
        /// <param name="worker">Job to run.</param>
        /// <returns>A Future&lt;&gt; that will hold the result.</returns>
        public Future<TData> QueueWorker<TData>(Func<TData> worker)
        {
            return QueueWorker(0, worker);
        }

        /// <summary>
        /// Enqueue a job returning a value and provide a Future&lt;&gt; to
        /// retrieve the result.
        /// </summary>
        /// <typeparam name="TData">The type of the result.</typeparam>
        /// <param name="tag">Queue tag associated to the job.</param>
        /// <param name="worker">Job to run.</param>
        /// <returns>A Future&lt;&gt; that will hold the result.</returns>
        public Future<TData> QueueWorker<TData>(int tag, Func<TData> worker)
        {
            var future = new Future<TData>();
            QueueWorker(() =>
            {
                try
                {
                    future.Value = worker();
                }
                catch (Exception ex)
                {
                    future.Throw(ex);
                }
            });
            return future;
        }

        /// <summary>
        /// Required nupmber of threads in the pool.
        /// </summary>
        public int NThreads
        {
            get
            {
                return _wanted_n_of_threads;
            }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(_name + ": maximum number of threads cannot be zero or less.");
                }
                lock (_condition)
                {
                    _wanted_n_of_threads = value;
                }
            }
        }

        /// <summary>
        /// Number of pending jobs in the thread pool.
        /// </summary>
        public int Pending
        {
            get
            {
                lock (_condition)
                {
                    return _actions.Count;
                }
            }
        }

        /// <summary>
        /// Number of running jobs in the pool.
        /// </summary>
        public int Running
        {
            get
            {
                return _running_workers;
            }
        }

        /// <summary>
        /// Shutdown the thread pool. It cannot be restarted afterward.
        /// Current running jobs end normaly, pending jobs are not processed.
        /// </summary>
        public void Dispose()
        {
            HashSet<Thread> t;
            lock (_condition)
            {
                _disposing = true;
                t = new HashSet<Thread>(_threads);
                Monitor.PulseAll(_condition);
            }
            foreach (var thread in t)
                thread.Join(50);
        }

        /// <summary>
        /// The loop performed by threads in the pool. Pick elements
        /// in the FairQueue and run them until the pool is disposed.
        /// </summary>
        void RunWorkers()
        {
            try
            {
                while (!_disposing)
                {
                    Action running = null;
                    lock (_condition)
                    {
                        if (!_disposing && _actions.Empty)
                        {
                            Monitor.Wait(_condition);
                        }
                        if (!_disposing && !_actions.Empty)
                        {
                            running = _actions.Dequeue();
                        }
                    }
                    if (running != null)
                    {
                        try
                        {
                            Interlocked.Increment(ref _running_workers);
                            running();
                            Interlocked.Decrement(ref _running_workers);
                        }
                        finally
                        {
                        }
                    }

                    // Check new thread start / thread stop
                    if (!CheckThreads()) break;
                }
            }
            finally
            {
                lock (_condition)
                {
                    --_current_n_of_threads;
                    _threads.Remove(Thread.CurrentThread);
                }
            }
        }

        /// <summary>
        /// Starts threads if we're below the maximum number of threads, returns false otherwise.
        /// </summary>
        /// <returns>False if we must stop some threads, true otherwise.</returns>
        bool CheckThreads()
        {
            if (!_disposing && _current_n_of_threads != _wanted_n_of_threads)
            {
                lock (_condition)
                {
                    if (!_disposing && _current_n_of_threads != _wanted_n_of_threads)
                    {
                        if (_current_n_of_threads > _wanted_n_of_threads) return false;
                        else StartThreads();
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Start threads up to the maximum number for this pool.
        /// </summary>
        void StartThreads()
        {
            lock (_condition)
            {
                int nToLaunch = _wanted_n_of_threads - _current_n_of_threads;
                for (int i = 0; i < nToLaunch; ++i)
                {
                    var thread = new Thread(RunWorkers);
                    thread.Name = _name + " - thread #" + thread.ManagedThreadId;
                    thread.IsBackground = true;
                    _threads.Add(thread);
                    ++_current_n_of_threads;
                    thread.Start();
                }
            }
        }

        static long _id;

        readonly string _name;
        readonly object _condition = new object();
        readonly HashSet<Thread> _threads = new HashSet<Thread>();
        readonly FairQueue<Action> _actions = new FairQueue<Action>();
        int _running_workers;
        volatile int _current_n_of_threads;
        volatile int _wanted_n_of_threads;
        volatile bool _disposing;
    }
}
