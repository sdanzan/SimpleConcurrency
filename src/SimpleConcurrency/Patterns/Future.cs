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

namespace SimpleConcurrency.Patterns
{
    /// <summary>
    /// A holder for "future" values.
    /// </summary>
    /// <typeparam name="TData">The type of the underlying value.</typeparam>
    public sealed class Future<TData> : IWaitable
    {
        /// <summary>
        /// Access to the undelrying value.
        /// Read access will block until a value is set or an exception is transmitted.
        /// Write access will set the underlying value and signal all waiting threads.
        /// Once set the value cannot be changed and trying to do so will throw.
        /// </summary>
        public TData Value
        {
            get
            {
                lock (this)
                {
                    if (!_set)
                    {
                        Monitor.Wait(this);
                    }
                }

                if (_exception != null) throw new FutureValueException(_exception);
                return _data;
            }

            set
            {
                lock (this)
                {
                    if (_set) throw new FutureAlreadySetException();
                    _data = value;
                    _set = true;
                    Monitor.PulseAll(this);
                }
            }
        }

        /// <summary>
        /// Pass an exception to the Future and signal all waiting
        /// threads. The passed exception will be rethrown in all waiting threads.
        /// </summary>
        /// <param name="exception">The exception to signal to waiting threads.</param>
        public void Throw(Exception exception)
        {
            lock (this)
            {
                if (_set) throw new FutureAlreadySetException();
                _exception = exception;
                _set = true;
                Monitor.PulseAll(this);
            }
        }

        /// <summary>
        /// True if the Future is ready for access, false otherwise.
        /// Ready means either a value has been set or an exception transmitted.
        /// </summary>
        public bool IsSet
        {
            get
            {
                return _set;
            }
        }

        /// <summary>
        /// Wait for the Furture to be ready.
        /// </summary>
        public void Wait()
        {
            lock (this)
            {
                if (!_set)
                {
                    Monitor.Wait(this);
                }
            }
        }

        /// <summary>
        /// Wait for the Future to be ready, up to to a given delay.
        /// </summary>
        /// <param name="msec">Waiting delay in milliseconds.</param>
        /// <returns>True if the Future was ready before the delay, false otherwise.</returns>
        public bool Wait(int msec)
        {
            lock (this)
            {
                if (!_set)
                {
                    return Monitor.Wait(this, msec);
                }
            }
            return true;
        }

        /// <summary>
        /// Wait for the Future to be ready, up to to a given delay.
        /// </summary>
        /// <param name="msec">Waiting delay expressed as as TimeSpan.</param>
        /// <returns>True if the Future was ready before the delay, false otherwise.</returns>
        public bool Wait(TimeSpan span)
        {
            return Wait(span.Milliseconds);
        }

        TData _data;
        volatile Exception _exception;
        volatile bool _set;
    }

    /// <summary>
    /// An exception to signal exception occuring while setting a Future value.
    /// </summary>
    public class FutureValueException : Exception
    {
        public FutureValueException(Exception ex)
            : base("Exception while setting the value of a Future. Check inner exception to get the original exception.", ex)
        {
        }
    }

    /// <summary>
    /// An exception to signal multiple value set.
    /// </summary>
    public class FutureAlreadySetException : Exception
    {
        public FutureAlreadySetException()
            : base("Trying to set a value to an already set Future.")
        {
        }
    }
}