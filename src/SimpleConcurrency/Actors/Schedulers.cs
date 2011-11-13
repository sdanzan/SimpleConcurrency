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
using System.Threading;
using System.Threading.Tasks;

namespace SimpleConcurrency.Actors
{
    using SimpleConcurrency.Patterns;
    using SimpleConcurrency.ThreadPools;

    /// <summary>
    /// A simple scheduler using the standard .NET thread pool.
    /// </summary>
    public class StandardThreadPoolScheduler : IScheduler
    {
        /// <summary>
        /// Queue the provided Action to be exceuted as a worker by the .NET ThreadPool.
        /// </summary>
        /// <param name="action">Job to schedule.</param>
        public void Schedule(Action action)
        {
            ThreadPool.QueueUserWorkItem(o => action());
        }
    }

    /// <summary>
    /// A scheduler using a provided FairThreadPool
    /// </summary>
    public class FairThreadPoolScheduler : IScheduler
    {
        /// <summary>
        /// Standard constructor.
        /// </summary>
        /// <param name="pool">The FairThreadPool to use.</param>
        public FairThreadPoolScheduler(FairThreadPool pool)
        {
            _pool = pool;
        }

        /// <summary>
        /// Queue the provided Action into the underlying FairThreadPool.
        /// </summary>
        /// <param name="action">Job to schedule.</param>
        public void Schedule(Action action)
        {
            _pool.QueueWorker(action);
        }

        FairThreadPool _pool;
    }
}
