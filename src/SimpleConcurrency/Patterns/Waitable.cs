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

namespace SimpleConcurrency.Patterns
{
    /// <summary>
    /// A simple interface "waitable" objects.
    /// </summary>
    public interface IWaitable
    {
        /// <summary>
        /// Wait until signaled.
        /// </summary>
        void Wait();

        /// <summary>
        /// Wait until signaled or the specified delay is spent.
        /// </summary>
        /// <param name="msec">A maximum delay in milliseconds to wait for the signal.</param>
        /// <returns>true if return occurs before the delay, false otherwise</returns>
        bool Wait(int msec);

        /// <summary>
        /// Wait until signaled or the specified delay is spent.
        /// </summary>
        /// <param name="span">A maximum delay to wait for the signal.</param>
        /// <returns>true if return occurs before the delay, false otherwise</returns>
        bool Wait(TimeSpan span);
    }
}
