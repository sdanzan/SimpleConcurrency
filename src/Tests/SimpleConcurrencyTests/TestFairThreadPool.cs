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
using NUnit.Framework;

namespace SimpleConcurrencyTests.ThreadPools
{
    using SimpleConcurrency.ThreadPools;
    using SimpleConcurrency.Patterns;

    [TestFixture]
    [Category("SimpleConcurrency/FairThreadPool")]
    class TestFairThreadPool
    {
        static Random rng = new Random();

        /// <summary>
        /// Build a thread pool and dispose it.
        /// Check that state was ok after creation.
        /// </summary>
        [Test]
        public void TestLifeCycle()
        {
            using (var ftp = new FairThreadPool("Glube", 8))
            {
                Assert.AreEqual(8, ftp.NThreads, "Bad number of started threads");
                Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
                Assert.AreEqual(0, ftp.Running, "There should not be any running job");
            }
        }

        /// <summary>
        /// Enqueue a bunch of jobs and check that all of them finish normally.
        /// </summary>
        [Test]
        public void TestQueueWorker()
        {
            using (var ftp = new FairThreadPool("Glube", 8))
            {
                using (var finished = new ManualResetEvent(false))
                {
                    int countdown = 42;
                    for (int i = 0; i < 42; ++i)
                    {
                        ftp.QueueWorker(
                            rng.Next(12),
                            () =>
                            {
                                Assert.Greater(countdown, 0, "Incorrect countdown value, some jobs may have run more than once");
                                if (Interlocked.Decrement(ref countdown) == 0) finished.Set();
                            });
                    }
                    finished.WaitOne();
                    Assert.AreEqual(0, countdown, "Incorrect countdown value, some jobs may not have run or run more than once");
                    Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
                }
            }
        }

        /// <summary>
        /// Enqueue a bunch of jobs in waitable mode and wait for all of them to finish.
        /// Check that all went nicely.
        /// </summary>
        [Test]
        public void TestQueueWorkerWaitable()
        {
            using (var ftp = new FairThreadPool("Glube", 8))
            {
                int countdown = 42;
                var toWait = new List<IWaitable>();
                for (int i = 0; i < 42; ++i)
                {
                    toWait.Add(ftp.QueueWaitableWorker(
                        rng.Next(7),
                        () =>
                        {
                            Assert.Greater(countdown, 0, "Incorrect countdown value, some jobs may have run more than once");
                            Interlocked.Decrement(ref countdown);
                        }));
                }
                foreach (var w in toWait)
                {
                    w.Wait();
                }
                Assert.AreEqual(0, countdown, "Incorrect countdown value, some jobs may not have run or run more than once");
                Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
            }
        }

        /// <summary>
        /// Enqueue a "long" job that will block until told to continue.
        /// Check that waiting for the job times out before unblocking it.
        /// </summary>
        [Test]
        public void TestQueueWorkerWaitableTimeout()
        {
            using (var ftp = new FairThreadPool("Glube", 8))
            {
                using (var toggle = new ManualResetEvent(false))
                {
                    var ww = ftp.QueueWaitableWorker(() => toggle.WaitOne());
                    Assert.IsFalse(ww.Wait(10), "The timeout should have expired");
                    Assert.IsFalse(ww.Wait(new TimeSpan(0, 0, 0, 0, 10)), "The timeout should have expired");
                    toggle.Set();
                    ww.Wait();
                }

                Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
            }
        }

        /// <summary>
        /// Enqueue a bunch of future returning jobs and check that values
        /// and exceptions are correctly returned through the futures.
        /// </summary>
        [Test]
        public void TestQueueWorkerFuture()
        {
            using (var ftp = new FairThreadPool("Glube", 8))
            {
                var now = DateTime.UtcNow;
                int intToCheck = rng.Next();

                var futureInt = ftp.QueueWorker(() => intToCheck);
                var futureStr = ftp.QueueWorker(rng.Next(12), () => "Forty two");
                var futureDateTime = ftp.QueueWorker(rng.Next(), () => DateTime.UtcNow);
                var futureExc = ftp.QueueWorker<int>(() => { throw new InvalidOperationException(); });

                Assert.AreEqual(intToCheck, futureInt.Value, "The Future returned a bad value.");
                Assert.AreEqual("Forty two", futureStr.Value, "The Future returned a bad value.");
                Assert.GreaterOrEqual(futureDateTime.Value, now, "The Future should return a date in the future!");

                int r_e = rng.Next ();
                int r_f = r_e;
                var ex = Assert.Throws(typeof(FutureValueException), () => { r_f = futureExc.Value; }, "Exception type is incorrect.");
                Assert.IsTrue(ex.InnerException is InvalidOperationException);
                Assert.AreEqual(r_e, r_f, "The Future should not transmit a value.");

                Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
            }
        }

        /// <summary>
        /// Enqueue a "long" job returning a future that will block until told
        /// to resume. Check that waiting for the future to be ready times out before
        /// resuming the job.
        /// </summary>
        [Test]
        public void TestQueueWorkerFutureTimeout()
        {
            using (var ftp = new FairThreadPool("Glube", 8))
            {
                using (var toggle = new ManualResetEvent(false))
                {
                    var futureInt = ftp.QueueWorker(() => { toggle.WaitOne(); return 42; });
                    Assert.IsFalse(futureInt.Wait(new TimeSpan(0, 0, 0, 0, 10)), "The Future should time out.");
                    Assert.IsFalse(futureInt.Wait(10), "The Future should time out.");
                    toggle.Set();
                    Assert.AreEqual(42, futureInt.Value, "The Future returned a bad value.");
                }

                Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
            }
        }
    }
}
