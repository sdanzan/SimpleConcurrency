using System;

namespace GeneralExamples
{
    using SimpleConcurrency.ThreadPools;
    using SimpleConcurrency.Patterns;

    class Program
    {
        static void Main(string[] args)
        {
            var pool = new FairThreadPool("Example", Environment.ProcessorCount);

            Console.WriteLine(pool.Name);

            var future = pool.QueueWorker(() => 40);
            int r = 2 + future.Value;
            Console.WriteLine(r);
            Console.WriteLine("Press [ENTER] to exit.");
            Console.ReadLine();

            pool.Dispose();
        }
    }
}
