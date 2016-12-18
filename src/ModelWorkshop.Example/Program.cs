using ModelWorkshop.Scheduling;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModelWorkshop.Example
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using (var scheduler = new Scheduler<MyItem>(SchedulerCallback))
            {
                scheduler.Error += Scheduler_Error;
                scheduler.SchedulerError += Scheduler_SchedulerError;

                Parallel.Invoke(Enumerable.Repeat<Action>(() =>
                {
                    for (int i = 0; i < 10; i++)
                        scheduler.AddAndRun(new MyItem(Thread.CurrentThread.ManagedThreadId));
                }, 10).ToArray());

                Console.WriteLine("Pressy any key to cancel.");
                Console.ReadKey(true);

                scheduler.Stop();

                Console.WriteLine("Pressy any key to exit.");
                Console.ReadKey(true);
            }
        }

        private static void SchedulerCallback(MyItem item)
        {
            Console.Write("Source Thread ID: ");
            Console.WriteLine(item.SourceThreadID);
            Console.Write("Current Thread ID: ");
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("======================");
            Thread.Sleep(300);
        }

        private static void Scheduler_Error(object sender, ErrorEventArgs e)
        {
            Console.Error.WriteLine(e.Error);
        }

        private static void Scheduler_SchedulerError(object sender, SchedulerErrorEventArgs<MyItem> e)
        {
            Console.Error.WriteLine(e.Error);
        }
    }
}
