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

namespace SimpleConcurrency.Actors
{
    using SimpleConcurrency.Patterns;

    /// <summary>
    /// A base class for very simple actors. The chosen pattern 
    /// is largely inspired by Scala actors.  Actor can "Receive"
    /// messages (blocking wait) or "React" to them (non blocking wait).
    /// 
    /// Messages are always processed one at a time, which means
    /// that an actor is only active in one thread at a time.
    /// 
    /// Actually the present code was more or less written as proof of 
    /// concept to check if Scala-like actors were feasible in plain C# 
    /// while keeping a simple interface, good performances, and a simple
    /// code (well, kind of).
    /// 
    /// Message processing loops can be written in two flavors:
    ///     - Iterative style using 'Receive' calls in a while construct,
    ///     - Functional style using recursive React calls, LoopReact, or
    ///       Act recalls.
    /// 
    /// For SimpleActor stressing you may have a look at the Ring
    /// Benchmark provided in the samples.
    /// 
    /// TODO: 'Stop' method, replies via Future, simplify internals.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messzges.</typeparam>
    public abstract class SimpleActor<TMessage>
    {
        /// <summary>
        /// Default constructor. SimpleActo activity will be scheduled
        /// using the .NET ThreadPool.
        /// </summary>
        protected SimpleActor()
        {
            _scheduler = new StandardThreadPoolScheduler();
        }

        /// <summary>
        /// A constructor allowing use of a given scheduler.
        /// </summary>
        /// <param name="scheduler">A schedule to run the actor's activity.</param>
        protected SimpleActor(IScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        /// <summary>
        /// Start the actor, i.e. schedule the 'Act' method to run.
        /// </summary>
        public void Start()
        {
            _scheduler.Schedule(Act);
        }

        /// <summary>
        /// Send a message to the actor.
        /// </summary>
        /// <param name="message">Message to pass to the actor.</param>
        public void Post(TMessage message)
        {
            Post(message, null);
        }

        /// <summary>
        /// Send a message to the actor.
        /// </summary>
        /// <param name="message">Message to pass do the actor.</param>
        /// <param name="sender">The originator of the message.</param>
        public void Post(TMessage message, SimpleActor<TMessage> sender)
        {
            lock (_messages)
            {
                _messages.Enqueue(new Tuple<TMessage, SimpleActor<TMessage>>(message, sender));
                switch (_state)
                {
                    case State.Receiving:
                        Monitor.Pulse(_messages);
                        break;

                    case State.PendingReact:
                        _state = State.Reacting;
                        _scheduler.Schedule(InnerReact);
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Must be implemented ot define the behaviour of the actor.
        /// </summary>
        protected abstract void Act();

        /// <summary>
        /// Wait for a message to be posted. The message will be handled
        /// with the provided closure. This is a non blocking method, and the
        /// actor won't monopolize a thread while waiting. Calling 'React'
        /// or 'Receive' while another 'React' is pending will throw
        /// a ActorAlreadySuspended exception.
        /// 
        /// Note that contrary to Scala's react, this implementation does indeed
        /// return (see comments in code about this).
        /// </summary>
        /// <param name="handler">The handler for when a message is received.</param>
        protected void React(Action<TMessage> handler)
        {
            React((m, s) => handler(m));

            // Yes we really get there. To simulate a non-returning React we'd have
            // to use an exception to non-local goto out of the calling scope, but
            // I'm not so sure about doing that in C#: exceptions are a real hindrance
            // to performance that would probably kill the LoopReact pattern and
            // anyway client code can easily break by catching/finally the exception and
            // fucking itself. I'll make some tests anyway (there are ways to avoid
            // the performance problem).
        }

        /// <summary>
        /// Wait for a message to be posted. The message will be handled
        /// with the provided closure. This is a non blocking method, and the
        /// actor won't monopolize a thread while waiting. Calling 'React'
        /// or 'Receive' while another 'React' is pending will throw
        /// a ActorAlreadySuspended exception.
        /// 
        /// This form allow the originator of the message to be passed when
        /// reacting.
        /// </summary>
        /// <param name="handler">The handler for when a message is received.</param>
        protected void React(Action<TMessage, SimpleActor<TMessage>> handler)
        {
            lock (_messages)
            {
                if (_state == State.Receiving || _state == State.PendingReact) throw new ActorAlreadySuspended();
                if (_state != State.Reacting) _state = State.PendingReact;
                _currentHandler = handler;
                if (_messages.Count > 0 && _state != State.Reacting)
                {
                    _state = State.Reacting;
                    _scheduler.Schedule(InnerReact);
                }
            }
        }

        /// <summary>
        /// Start a non blocking loop of 'React' with the same handler
        /// until the handler returns false.
        /// 
        /// Check the samples to see an alternate way of doing that
        /// using the Y Combinator trick.
        /// 
        /// Obviously you can also loop by recalling the Act() method.
        /// </summary>
        /// <param name="handler">A handler repeatedly reenqueued in React
        /// until it returns false.</param>
        protected void LoopReact(Func<TMessage, bool> handler)
        {
            LoopReact((m, s) => handler(m));
        }

        /// <summary>
        /// Start a non blocking loop of 'React' with the same handler,
        /// until the hanlder returns false. Allow access to the originator
        /// of the message.
        /// </summary>
        /// <param name="handler">A handler repeatedly reenqueued in React
        /// until it returns false.</param>
        protected void LoopReact(Func<TMessage, SimpleActor<TMessage>, bool> handler)
        {
            React((m, s) =>
            {
                if (handler(m, s)) LoopReact(handler);
            });
        }

        /// <summary>
        /// Wait for a message to be posted while blocking the calling thread.
        /// The function only returns when a message is available. Calling 'React' or
        /// 'Receive' while another 'Receive' is pending wil throw a
        /// ActorAlreadySuspended exception. That means a receive handler cannot
        /// make another call to 'Receive'.
        /// </summary>
        /// <param name="handler">Handler to process the received message.</param>
        protected void Receive(Action<TMessage> handler)
        {
            Receive((m, s) => handler(m));
        }

        /// <summary>
        /// Wait for a message to be posted while blocking the calling thread.
        /// The function only returns when a message is available. Calling 'React' or
        /// 'Receive' while another 'Receive' is pending wil throw a
        /// ActorAlreadySuspended exception. That means a receive handler cannot
        /// make another call to 'Receive' (se coments in the code).
        /// 
        /// That form allow access to the originator of the message.
        /// </summary>
        /// <param name="handler">Handler to process the received message.</param>
        protected void Receive(Action<TMessage, SimpleActor<TMessage>> handler)
        {
            Tuple<TMessage, SimpleActor<TMessage>> message;
            lock (_messages)
            {
                // Cannot recursively call 'Receive'. This is to avoid
                // stack overflow errors when messing up. Write a loop
                // your 'Receive' calls or prefer 'React' for recursion.
                if (_state != State.Inactive) throw new ActorAlreadySuspended();
                _state = State.Receiving;
                if (_messages.Count == 0)
                {
                    Monitor.Wait(_messages);
                }
                message = _messages.Dequeue();
            }
            try
            {
                handler(message.Item1, message.Item2);
            }
            finally
            {
                _state = State.Inactive;
            }
        }

        /// <summary>
        /// Implementation of the React subtleties. Yeah, the code is
        /// quite convoluted, I'm not really pleased with it. 
        /// </summary>
        private void InnerReact()
        {
            // Code in there works because 'React' is only called
            // in 'Act' or from a handler called from 'InnerReact'.
            // If you break the "only active in one thread at a time" rule
            // for actors when you overload 'Act', you're doing it wrong.
            try
            {
                // We will loop over the message queue as long as
                // there is still a handler. _currentHandler is set to
                // null before handling the message, so if it comes out as
                // non null again just after, that means the handler made a
                // "recursive" call to 'React'. Continuing processing messages
                // avoids useless rescheduling. However we will process at most
                // the number of messages present in the queue the first time
                // we enter the process loop, and reschedule ourself after that.
                // This is to avoid having a single Actor monopolizing a thread.
                int loop = -1;
                do
                {
                    Tuple<TMessage, SimpleActor<TMessage>> message;
                    lock (_messages)
                    {
                        int count = _messages.Count;
                        if (loop == -1) loop = count;

                        if (count == 0)
                        {
                            // No more messages => quit the loop
                            _state = State.PendingReact;
                            return;
                        }

                        if (loop == 0)
                        {
                            // We've process enough messages in row => reschedule to later
                            _scheduler.Schedule(InnerReact);
                            return;
                        }

                        message = _messages.Dequeue();
                    }
                    --loop;
                    var handler = _currentHandler;
                    _currentHandler = null;
                    handler(message.Item1, message.Item2);
                }
                while (_currentHandler != null);
                lock (_messages)
                {
                    _state = State.Inactive;
                }
            }
            catch
            {
                lock (_messages)
                {
                    _state = State.Inactive;
                    _currentHandler = null;
                }
                throw;
            }
        }

        enum State
        {
            Inactive,
            Receiving,
            PendingReact,
            Reacting
        }

        volatile State _state = State.Inactive;
        Queue<Tuple<TMessage, SimpleActor<TMessage>>> _messages = new Queue<Tuple<TMessage, SimpleActor<TMessage>>>();
        Action<TMessage, SimpleActor<TMessage>> _currentHandler;
        IScheduler _scheduler;
    }

    /// <summary>
    /// Exception thrown when trying to 'Receive' or 'React'
    /// while there are already pending ones.
    /// </summary>
    public class ActorAlreadySuspended : Exception
    {
    }
}
