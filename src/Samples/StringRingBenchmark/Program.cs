using System;
using System.Threading;
using System.Diagnostics;

/*
 * This is a thread ring benchmark as described here:
 *      http://groovy.dzone.com/articles/why-scala-actors-15-20-times
 * 
 * I added the possibility to force the actors to consume a little cpu
 * each time they receive a message.
 * 
 * */

namespace StringRingBenchmark
{
    using SimpleConcurrency.Actors;
    using SimpleConcurrency.Patterns;
    using SimpleConcurrency.ThreadPools;

    class Program
    {
        static class Config
        {
            public static int NB_ACTORS = 10000;
            public static int NMESSAGES = 500;
            public static int Threads;
            public static bool Force = false;
            public static Player[] Players;
        }

        static class State
        {
            public static int Posted = 0;
            public static int Received = 0;
            public static ManualResetEvent FinishSignal = new ManualResetEvent(false);
        }

        class Player : SimpleActor<string>
        {
            public Player(int id, IScheduler sched)
                : base(sched)
            {
                _id = id;
            }

            protected override void Act()
            {
                LoopReact(m =>
                {
                    if (_id < Config.NB_ACTORS - 1)
                    {
                        Config.Players[_id + 1].Post(m);
                        Interlocked.Increment(ref State.Posted);
                    }
                    else
                    {
                        if (Interlocked.Increment(ref State.Received) == Config.NMESSAGES)
                            State.FinishSignal.Set();
                    }
                    if (Config.Force)
                        for (int i = 0; i < 500000; ++i) ;
                    return true;
                });
            }

            int _id;
        }

        static void Usage()
        {
            Console.WriteLine(@"
Usage: StringRingBenchmark [-f NTHREADS] [-e] [-n MESSAGES] [-r RING]
    --help              Print this message.
    -f NTHREADS         Do not use the standard scheduler, instead use a FairThreadPoolScheduler with NTHREADS threads if 
                        NTHREADS > 0, and a TaskScheduler if NTHREADS < 0.            
    -m MESSAGES         Number of messages sent acrros the ring. Default is 500.
    -r RING             Size of the ring. Default is 10000.
    -c                  Force actors to spend some time in a cpu loop each time they receive a message.
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

                        case "-r":
                            Config.NB_ACTORS = int.Parse(args[++i]);
                            break;

                        case "-m":
                            Config.NMESSAGES = int.Parse(args[++i]);
                            break;

                        case "-c":
                            Config.Force = true;
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

            Config.Players = new Player[Config.NB_ACTORS];

            IScheduler sched;

            if (Config.Threads > 0)
            {
                sched = new FairThreadPoolScheduler(new FairThreadPool("ThreadRing", Config.Threads));
            }
            else if (Config.Threads == 0)
            {
                sched = new StandardThreadPoolScheduler();
            }
            else
            {
                sched = new TaskScheduler();
            }

            for (int i = 0; i < Config.NB_ACTORS; ++i)
            {
                Config.Players[i] = new Player(i + 1, sched);
                Config.Players[i].Start();
            }

            Console.WriteLine("=== String ThreadRing ===");
            Console.WriteLine("Messages: " + Config.NMESSAGES);
            Console.WriteLine("Ring size: " + Config.NB_ACTORS);
            Console.WriteLine("Using {0}", Config.Threads == 0 ? "Standard .NET ThreadPool" : Config.Threads < 0 ? "Task Scheduler" : "FairThreadPool (" + Config.Threads + ")");

            for (int i = 0; i < Config.NMESSAGES; ++i)
                Config.Players[i].Post("Hi");

            State.FinishSignal.WaitOne();
            Console.WriteLine("Posted: " + State.Posted);
            Console.WriteLine(chrono.Elapsed);
            Environment.Exit(0);
        }
    }
}
