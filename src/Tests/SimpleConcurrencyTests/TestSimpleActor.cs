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

namespace SimpleConcurrencyTests.Actors
{
    using SimpleConcurrency.Actors;
    using SimpleConcurrency.Patterns;

    class ReactTestActor : SimpleActor<string>
    {
        public ReactTestActor(Action<string, SimpleActor<string>> handler)
        {
            _handler = handler;
        }

        protected sealed override void Act()
        {
            React(_handler);
        }

        Action<string, SimpleActor<string>> _handler;
    }

    class LoopReactTestActor : SimpleActor<string>
    {
        public LoopReactTestActor(Func<string, SimpleActor<string>, bool> handler)
        {
            _handler = handler;
        }

        protected sealed override void Act()
        {
            LoopReact(_handler);
        }

        Func<string, SimpleActor<string>, bool> _handler;
    }

    class ReceiveTestActor : SimpleActor<string>
    {
        public ReceiveTestActor(Action<string, SimpleActor<string>> handler)
        {
            _handler = handler;
        }

        protected sealed override void Act()
        {
            Receive(_handler);
        }

        Action<string, SimpleActor<string>> _handler;
    }

    [TestFixture]
    [Category("SimpleConcurrency/Actors")]
    class TestSimpleActor
    {
        [Test]
        public void TestReceive()
        {
            var message = new Future<string>();
            var sender = new Future<SimpleActor<string>>();
            var receiver = new ReceiveTestActor((m, s) => { message.Value = m; sender.Value = s; });
            receiver.Start();

            receiver.Post("Youhou");
            Assert.AreEqual("Youhou", message.Value);
            Assert.AreEqual(null, sender.Value);

            var r1 = receiver;
            message = new Future<string>();
            sender = new Future<SimpleActor<string>>();
            receiver = new ReceiveTestActor((m, s) => { message.Value = m; sender.Value = s; });
            receiver.Start();

            receiver.Post("Youhou", r1);
            Assert.AreEqual("Youhou", message.Value);
            Assert.AreEqual(r1, sender.Value);
        }

        [Test]
        public void TestReact()
        {
            var message = new Future<string>();
            var sender = new Future<SimpleActor<string>>();
            var receiver = new ReactTestActor((m, s) => { message.Value = m; sender.Value = s; });
            receiver.Start();

            receiver.Post("Youhou");
            Assert.AreEqual("Youhou", message.Value);
            Assert.AreEqual(null, sender.Value);

            var r1 = receiver;
            message = new Future<string>();
            sender = new Future<SimpleActor<string>>();
            receiver = new ReactTestActor((m, s) => { message.Value = m; sender.Value = s; });
            receiver.Start();

            receiver.Post("Youhou", r1);
            Assert.AreEqual("Youhou", message.Value);
            Assert.AreEqual(r1, sender.Value);
        }
    }
}
