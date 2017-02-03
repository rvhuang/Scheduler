using ModelWorkshop.Scheduling;
using ModelWorkshop.Scheduling.Redis;
using StackExchange.Redis;
using System;
using System.Diagnostics;

namespace ModelWorkshop.Example.Redis
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // We recommend to run multiple instances of the example.

            var processId = Process.GetCurrentProcess().Id;

            Console.Title = string.Format("Process ID: {0}", processId);

            using (var conn = ConnectionMultiplexer.Connect("localhost:6379"))
            using (var scheduler = new Scheduler<Request>(SchedulerCallback, new ObservableRedisStack<Request>(conn, "test", 0)))
            {
                scheduler.Error += Scheduler_Error;
                scheduler.SchedulerError += Scheduler_SchedulerError;

                for (int i = 0; i < 10000; i++)
                    scheduler.AddAndRun(new Request()
                    {
                        Date = DateTime.Today,
                        GUID = Guid.NewGuid(),
                        ProcessId = processId,
                        Timestamp = Stopwatch.GetTimestamp()
                    });

                Console.WriteLine("Pressy any key to cancel.");
                Console.ReadKey(true);

                scheduler.Stop();

                Console.WriteLine("Pressy any key to exit.");
                Console.ReadKey(true);
            }
        }

        private static void SchedulerCallback(Request item)
        {
            Console.WriteLine("Received Request from Process: {0}", item.ProcessId);
            Console.WriteLine("======================");
        }

        private static void Scheduler_Error(object sender, ErrorEventArgs e)
        {
            Console.Error.WriteLine(e.Error);
        }

        private static void Scheduler_SchedulerError(object sender, SchedulerErrorEventArgs<Request> e)
        {
            Console.Error.WriteLine(e.Error);
        }
    }
}
