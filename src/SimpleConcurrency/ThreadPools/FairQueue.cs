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

namespace SimpleConcurrency.ThreadPools
{
    /// <summary>
    /// A "fair" queue.
    /// Data are enqueued associated to a "tag" and each tag gets its own queue. A tag is essentially a queue id
    /// provided by the client.
    /// Data is dequeued by round robin cycling between all the tagged queues each time an element is dequeued.
    /// That means no tag can "block" the dequeueing of elements bound to another tag. Data in the same tagged
    /// queue is dequeued in fifo order. 
    /// Enqueuing and Dequeuing are both O(1).
    /// This class is not thread safe, if needed synchronization burden belongs to the caller.
    /// </summary>
    class FairQueue<TData>
    {
        /// <summary>
        /// Enqueue some data. O(1) operation.
        /// </summary>
        /// <param name="tag">Tag associated to the data.</param>
        /// <param name="data">Data to enqueue.</param>
        public void Enqueue(int tag, TData data)
        {
            LinkedQueue tagged;
            if (!_queues.TryGetValue(tag, out tagged))
            {
                tagged = new LinkedQueue(tag, new Queue<TData>());
                _queues[tag] = tagged;
            }
            if (tagged.Content.Count == 0)
            {
                if (_tail != null)
                {
                    _tail.Next = tagged;
                    _tail = tagged;
                }
                else
                {
                    _head = _tail = tagged;
                }
            }
            tagged.Content.Enqueue(data);
            ++_count;
        }

        /// <summary>
        /// Enqueue some data associated to tag 0. O(1) operation.
        /// </summary>
        /// <param name="data">Data to enqueue.</param>
        public void Enqueue(TData data)
        {
            Enqueue(0, data);
        }

        /// <summary>
        /// Dequeue some data. You cannot predict the tag the next dequeued data belongs to, but data belonging
        /// to the same tag is guaranteed to come out in fifo order.
        /// O(1) operation.
        /// </summary>
        /// <returns>Dequeued data.</returns>
        public TData Dequeue()
        {
            int tag;
            return Dequeue(out tag);
        }

        /// <summary>
        /// Dequeue some data. You cannot predict the tag the next dequeued data belongs to, but data belonging
        /// to the same tag is guaranteed to come out in fifo order.
        /// O(1) operation.
        /// </summary>
        /// <param name="tag">The tag to which the data belonged to.</param>
        /// <returns>Dequeued data.</returns>
        public TData Dequeue(out int tag)
        {
            if (_head == null) throw new InvalidOperationException("Trying to dequeue from an empty FairQueue.");

            var data = _head.Content.Dequeue();
            tag = _head.Tag;

            var oldHead = _head;
            if (oldHead != _tail)
            {
                _head = oldHead.Next;
                oldHead.Next = null;
                if (oldHead.Content.Count != 0)
                {
                    _tail.Next = oldHead;
                    _tail = oldHead;
                }
            }
            else
            {
                if (oldHead.Content.Count == 0)
                {
                    _head = _tail = null;
                }
            }

            --_count;
            return data;
        }

        /// <summary>
        /// Number of elements in the FairQueue.
        /// O(1) operation.
        /// </summary>
        public int Count
        {
            get
            {
                return _count;
            }
        }

        /// <summary>
        /// Number of elements associated to a given tag in the FairQueue.
        /// O(1) operation.
        /// </summary>
        /// <param name="tag">The tag to count elements from.</param>
        /// <returns>Number of elements associated to the given tag.</returns>
        public int CountTagged(int tag)
        {
            return _queues[tag].Content.Count;
        }

        /// <summary>
        /// Returns true if the FairQueue is empty, false otherwise.
        /// O(1) operation.
        /// </summary>
        public bool Empty
        {
            get
            {
                return Count == 0;
            }
        }

        /// <summary>
        /// Basic linked list of queues to round robin order the dequeing operations.
        /// </summary>
        private class LinkedQueue
        {
            public LinkedQueue(int tag, Queue<TData> self)
            {
                Tag = tag;
                Content = self;
            }

            public readonly int Tag;
            public readonly Queue<TData> Content;
            public LinkedQueue Next;
        }

        readonly Dictionary<int, LinkedQueue> _queues = new Dictionary<int, LinkedQueue>();
        LinkedQueue _head;
        LinkedQueue _tail;
        int _count;
    }
}
