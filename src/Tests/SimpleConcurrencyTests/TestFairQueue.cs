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
using NUnit.Framework;

namespace SimpleConcurrencyTests.ThreadPools
{
    using SimpleConcurrency.ThreadPools;

    [TestFixture]
    [Category("SimpleConcurrency/FairQueue")]
    class TestFairQueue
    {
        /// <summary>
        /// Basic enqueueing tests.
        /// </summary>
        [Test]
        public void TestEnqueue()
        {
            var fq = new FairQueue<string>();
            fq.Enqueue("1");
            Assert.AreEqual(1, fq.Count, "Incorrect number of elements in the queue");
            Assert.IsFalse(fq.Empty, "The queue should not be empty");

            fq.Enqueue("2");
            Assert.AreEqual(2, fq.Count);
            Assert.IsFalse(fq.Empty);

            fq.Enqueue(1, "3");
            Assert.AreEqual(3, fq.Count, "Incorrect number of elements in the queue");
            Assert.IsFalse(fq.Empty, "The queue should not be empty");

            fq.Enqueue(2, "4");
            fq.Enqueue(2, "5");
            Assert.AreEqual(5, fq.Count, "Incorrect number of elements in the queue");
            Assert.IsFalse(fq.Empty, "The queue should not be empty");
        }

        /// <summary>
        /// Basic dequeueing tests.
        /// </summary>
        [Test]
        public void TestDequeue()
        {
            var fq = new FairQueue<string>();
            fq.Enqueue("1");
            var r = fq.Dequeue();
            Assert.IsTrue(fq.Empty, "The queue should be empty");
            Assert.AreEqual("1", r, "Incorrect dequeued value");

            fq.Enqueue("1");
            fq.Enqueue(2, "2");
            fq.Enqueue(2, "3");
            fq.Enqueue(5, "4");
            fq.Enqueue("5");
            fq.Dequeue();
            fq.Dequeue();
            fq.Dequeue();
            fq.Dequeue();
            fq.Dequeue();
            Assert.IsTrue(fq.Empty, "The queue should be empty");

            Assert.Throws(typeof(InvalidOperationException), () => fq.Dequeue(), "Dequeueing from an empty queue should throw an InvalidOperationException");

            int tag;
            fq.Enqueue("1");
            fq.Dequeue(out tag);
            Assert.AreEqual(0, tag, "The queue is supposed to contain only one tag and we got an incorrect value for it");
            fq.Enqueue(42, "1");
            fq.Dequeue(out tag);
            Assert.AreEqual(42, tag, "The queue is supposed to contain only one tag and we got an incorrect value for it");
        }

        /// <summary>
        /// "Fairness" test. Checks that dequeueing operations alternate
        /// between all tags.
        /// </summary>
        [Test]
        public void TestFairness()
        {
            var fq = new FairQueue<string>();
            int enqueued = 0;
            fq.Enqueue(1, "11"); ++enqueued;
            fq.Enqueue(1, "12"); ++enqueued;
            fq.Enqueue(1, "13"); ++enqueued;
            fq.Enqueue(1, "14"); ++enqueued;

            fq.Enqueue(2, "21"); ++enqueued;
            fq.Enqueue(2, "22"); ++enqueued;
            fq.Enqueue(2, "23"); ++enqueued;
            fq.Enqueue(2, "24"); ++enqueued;

            fq.Enqueue(3, "31"); ++enqueued;
            fq.Enqueue(3, "32"); ++enqueued;
            fq.Enqueue(3, "33"); ++enqueued;
            fq.Enqueue(3, "34"); ++enqueued;

            int tag, ptag;
            fq.Dequeue(out tag); --enqueued;
            for (int i = 0; i < enqueued; ++i)
            {
                ptag = tag;
                fq.Dequeue(out tag);
                Assert.AreEqual((ptag + 1) % 3, tag % 3, "Incorrect cycling between tags");
            }

            Assert.IsTrue(fq.Empty, "The queue should be empty");
        }
    }
}
