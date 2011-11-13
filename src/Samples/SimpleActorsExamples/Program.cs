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

namespace SimpleActorsExamples
{
    using HelloWorld;
    using PingPong;
    using SimpleLoop;

    class Program
    {
        static void Ask()
        {
            Console.WriteLine("Press [ENTER] to continue");
            Console.ReadLine();
        }

        static void Main(string[] args)
        {
            try
            {
                var rng = new Random();

                var hello = new HelloWorldActor();
                hello.Start();
                hello.Post("Hello World!");

                Ask();

                var hello2 = new ReactHelloWorldActor();
                hello2.Start();
                hello2.Post("Hello World!");

                Ask();

                var loop = new SimpleLoopActor();
                loop.Start();
                loop.Post(rng.Next(42));

                Ask();

                var ping = new SimplePingPong();
                var pong = new SimplePingPong();
                ping.Start();
                pong.Start();
                ping.Post(PingPongMessage.Ping, pong);

                Ask();
                
                PingPongActor[] pp = new PingPongActor[2];
                pp[0] = new ReceivePingPongActor(rng.Next(3, 10));
                pp[1] = new ReactPingPongActor(rng.Next(3, 10));
                pp[0].Start();
                pp[1].Start();
                int idx = rng.Next(2);
                pp[idx].Post(PingPongMessage.Ping, pp[1 - idx]);

                Ask();

                pp[0] = new ReceivePingPongActor(rng.Next(3, 10));
                pp[1] = new YReactPingPongActor(rng.Next(3, 10));
                pp[0].Start();
                pp[1].Start();
                idx = rng.Next(2);
                pp[idx].Post(PingPongMessage.Ping, pp[1 - idx]);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("Press [ENTER] to quit");
            Console.ReadLine();
        }
    }
}
