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
using System.Diagnostics;

using SimpleConcurrency.Actors;
using SimpleConcurrency.Patterns;
using SimpleConcurrency.ThreadPools;

/*
 * This is an implementation of the ThreadRing and Parallel ThreadRing
 * as described here:
 *      [1] http://shootout.alioth.debian.org/u64q/performance.php?test=threadring
 *      [2] http://www.theron-library.com/index.php?t=page&p=parallelthreadring
 * 
 * For the normal ThreadRing I used the Erlang sample as comparison
 *      [3] http://shootout.alioth.debian.org/u64q/program.php?test=threadring&lang=erlang&id=1
 * 
 * I ran the test on an Intel i7 2600 3.4Ghz under Windows 7 (4 cores + hyper threading).
 * Erlang version was 5.8.5 OTP R14B04. Same options as in [3] were used.
 * 
 * The Erlang ThreadRing run in ~16.5 seconds.
 * 
 * This C# ThreadRing runs in about 1 min 15 with default options ('React' pattern, use .NET thread pool).
 * However when using '-f 1' option it goes down to 15 sec. The reason is that basically the ThreadRing is
 * sequential: only one actor is active at a time, so you really need only one thread to get
 * max perfs. Erlang does this automatically. With SimpleConcurrency framework you'll have
 * to use the 'React' pattern to get Erlang style actors (this benchmark uses that by default) and set
 * the actors to run in a scheduler with only one thread. If you use multiple threads you'll end doing
 * context switches most of the time (you could also use a better scheduler than the ones I use, The 
 * FairThreadPool does not attempt to minimize switching, and neither does the .NET ThreadPool). That
 * does not mean in any way that Simpleconcurrency does better than Erlang: the ThreadRing benchmark is
 * basically just an elaborated "for(i = N; i > 0; i--)" so if you force your framework to do that in a single
 * thread, obviously it should be fast.
 * 
 * The parallel ThreadRing is another thing entirely, with default options it runs in about 3 sec, with '-f 4' it
 * goes down to 2 sec, and with '-f 1' goes up to 5 sec. It scales linearly with the number of hops.
 * 
 * NOTE: with the Mono runtime on Mac, performances suck big time. I suspect the thread primitives to be horrible
 * and quite broken on mono/osx. On Linux it should work fine.
 */

namespace RingBenchmark
{
    static class Program
    {
        static class Config
        {
            public static int RING_SIZE = 503;
            public static int N_HOPS = 50000000;
            public static int Verbosity = 0;
            public static int Target = 0;

            public static bool Parallel = false;
            public static int Threads = 0;
            public static bool Receive = false;

            public static Player[] Players;
        }

        static class State
        {
            public static int Done_Hops = 0;
            public static ManualResetEvent EndFlag = new ManualResetEvent(false);
            public static int Completed = 0;
        }

        public static void Ack()
        {
            if (Interlocked.Increment(ref State.Completed) == Config.Target)
                State.EndFlag.Set();
        }

        class Player : SimpleActor<int>
        {
            public Player(int id, IScheduler scheduler, bool receive)
                : base(scheduler)
            {
                _id = id;
                _receive = receive;
            }

            protected override void Act()
            {
                if (_receive)
                {
                    while (true)
                    {
                        Receive(i =>
                        {
                            if (i <= 0)
                            {
                                Console.WriteLine(_id);
                                Ack();
                            }
                            else
                            {
                                if (Config.Verbosity > 0 && i % (Config.N_HOPS / Config.Verbosity) == 0)
                                    Console.WriteLine(i);
                                Config.Players[_id % Config.Players.Length].Post(i - 1);
                                Interlocked.Increment(ref State.Done_Hops);
                            }
                        });
                    }
                }
                else
                {
                    LoopReact(i =>
                    {
                        if (i <= 0)
                        {
                            Console.WriteLine(_id);
                            Ack();
                        }
                        else
                        {
                            if (Config.Verbosity > 0 && i % (Config.N_HOPS / Config.Verbosity) == 0)
                                Console.WriteLine(i);
                            Config.Players[_id % Config.Players.Length].Post(i - 1);
                            Interlocked.Increment(ref State.Done_Hops);
                        }
                        return true;
                    });
                }
            }

            int _id;
            bool _receive;
        }

        static void Usage()
        {
            Console.WriteLine(@"
Usage: RingBenchmark [-f NTHREADS] [-p] [-v FEEDBACK] [-e] [-h HOPS] [-r RING]
    --help              Print this message.
    -f NTHREADS         Do not use the standard .NET thread pool, instead use a FairThreadPool with NTHREADS threads.
    -p                  Run the parallel version of the ring. Instead of having one message starting at player 1
                        start with 1 message per player, so that RING messages are running around the ring.
    -v FEEDBACK         Print some feedback every HOPS/FEEDBACK message forwarding. Default is no feedback.
    -e                  Use 'Receive' instead of 'React' (=> thread pool will be sized accordingly).
    -h HOPS             Stop after HOPS forwarding of the message(s). Default is 50 000 000.
    -r RING             Size of the ring. Default is 503.
");
            Environment.Exit(-1);
        }

        static void ParseOptions(string[] args)
        {
            try
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    switch (args[i])
                    {
                        case "-f":
                            Config.Threads = int.Parse(args[++i]);
                            break;

                        case "-p":
                            Config.Parallel = true;
                            break;

                        case "-e":
                            Config.Receive = true;
                            break;

                        case "-r":
                            Config.RING_SIZE = int.Parse(args[++i]);
                            break;

                        case "-h":
                            Config.N_HOPS = int.Parse(args[++i]);
                            break;

                        case "-v":
                            Config.Verbosity = int.Parse(args[++i]);
                            break;

                        case "--help":
                        default:
                            Usage();
                            break;
                    }
                }
            }
            catch
            {
                Usage();
            }
        }

        static void Main(string[] args)
        {
            ParseOptions(args);

            Stopwatch chrono = Stopwatch.StartNew();

            Config.Players = new Player[Config.RING_SIZE];

            IScheduler sched;

            if (Config.Threads > 0)
            {
                sched = new FairThreadPoolScheduler(new FairThreadPool("ThreadRing", Config.Receive ? Config.RING_SIZE + Config.Threads : Config.Threads));
            }
            else
            {
                if (Config.Receive)
                    ThreadPool.SetMinThreads(Config.RING_SIZE + 2, Environment.ProcessorCount);
                sched = new StandardThreadPoolScheduler();
            }

            for (int i = 0; i < Config.RING_SIZE; ++i)
            {
                Config.Players[i] = new Player(i + 1, sched, Config.Receive);
                Config.Players[i].Start();
            }

            Console.WriteLine("=== {0}ThreadRing ===", Config.Parallel ? "Parallel " : "");
            Console.WriteLine("Messages: " + Config.N_HOPS);
            Console.WriteLine("Ring size: " + Config.RING_SIZE);
            Console.WriteLine("{0} mode", Config.Receive ? "'Receive'" : "'React'");
            Console.WriteLine("Using {0}", Config.Threads == 0 ? "Standard .NET ThreadPool" : "FairThreadPool (" + Config.Threads + ")");

            if (Config.Parallel)
            {
                Config.Verbosity = 0;
                Config.Target = Config.RING_SIZE;
                long hops1 = ((long)Config.N_HOPS + Config.RING_SIZE - 1) / Config.RING_SIZE;
                long hops2 = Config.N_HOPS - (Config.RING_SIZE - 1) * (((long)Config.N_HOPS + Config.RING_SIZE - 1) / Config.RING_SIZE);

                for (int i = 0; i < Config.RING_SIZE - 1; ++i)
                {
                    Config.Players[i].Post((int)hops1);
                }
                Config.Players[Config.RING_SIZE - 1].Post((int)hops2);
            }
            else
            {
                Config.Target = 1;
                Config.Players[0].Post(Config.N_HOPS);
            }

            State.EndFlag.WaitOne();
            Console.WriteLine("Posted: " + State.Done_Hops);
            Console.WriteLine(chrono.Elapsed);
            Environment.Exit(0);
        }
    }
}
