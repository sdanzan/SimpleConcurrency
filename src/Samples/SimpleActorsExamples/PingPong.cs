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
using SimpleConcurrency.Actors;

namespace SimpleActorsExamples.PingPong
{
    enum PingPongMessage
    {
        Ping,
        Pong,
        Stop
    }

    class SimplePingPong : SimpleActor<PingPongMessage>
    {
        protected override void Act()
        {
            int i = 0;
            LoopReact((message, sender) =>
            {
                Console.WriteLine(message);
                if (++i > 5 && sender != null)
                {
                    sender.Post(PingPongMessage.Stop, null);
                    return false;
                }
                switch (message)
                {
                    case PingPongMessage.Ping:
                        sender.Post(PingPongMessage.Pong, this);
                        return true;

                    case PingPongMessage.Pong:
                        sender.Post(PingPongMessage.Ping, this);
                        return true;

                    case PingPongMessage.Stop:
                        return false;
                }
                return true;
            });
        }
    }

    abstract class PingPongActor : SimpleActor<PingPongMessage>
    {
        string _name;
        int _max;
        int _received;

        protected PingPongActor(string name, int maxMessages)
        {
            _name = name;
            _max = maxMessages;
        }

        protected bool HandleMessage(PingPongMessage message, SimpleActor<PingPongMessage> sender)
        {
            Console.WriteLine(_name + ": " + message);
            if (++_received > _max)
            {
                sender.Post(PingPongMessage.Stop, this);
                Console.WriteLine(_name + ": " + _received + " messages received, I decided to stop.");
                return false;
            }
            else
            {
                switch (message)
                {
                    case PingPongMessage.Ping:
                        sender.Post(PingPongMessage.Pong, this);
                        break;

                    case PingPongMessage.Pong:
                        sender.Post(PingPongMessage.Ping, this);
                        break;

                    case PingPongMessage.Stop:
                        return false;

                    default:
                        break;
                }
            }
            return true;
        }
    }

    class ReceivePingPongActor : PingPongActor
    {
        public ReceivePingPongActor(int maxMessages)
            : base("Receive", maxMessages)
        {
        }

        protected override void Act()
        {
            bool loop = true;
            while (loop)
            {
                Receive((message, sender) => loop = HandleMessage(message, sender));
            }
        }
    }

    class ReactPingPongActor : PingPongActor
    {
        public ReactPingPongActor(int maxMessages)
            : base("React", maxMessages)
        {
        }

        protected override void Act()
        {
            LoopReact(HandleMessage);
        }
    }

    class YReactPingPongActor : PingPongActor
    {
        public YReactPingPongActor(int maxMessages)
            : base("YReact", maxMessages)
        {
        }

        static Action<M, S> Y_Combinator<M, S>(Func<Action<M, S>, Action<M, S>> fixer)
        {
            Action<M, S> fixed_func = null;
            fixed_func = fixer((m, s) => fixed_func(m, s));
            return fixed_func;
        }

        protected override void Act()
        {
#if __MonoCS__
            // Mono has a hard time infering correct types
            React(Y_Combinator<PingPongMessage, SimpleActor<PingPongMessage>>((Action<PingPongMessage, SimpleActor<PingPongMessage>> f) => (m, s) => {
                if (HandleMessage(m, s))
                    React(f);}));
#else
            React(Y_Combinator((Action<PingPongMessage, SimpleActor<PingPongMessage>> f) => (m, s) => { if (HandleMessage(m, s)) React(f); }));
#endif
        }
    }
}