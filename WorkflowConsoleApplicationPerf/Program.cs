using System;
using System.Activities;
using System.Activities.XamlIntegration;
using System.Collections.Generic;
using System.Threading;

namespace WorkflowConsoleApplicationPerf
{
    internal class ActivityMetadata
    {
        public int VersionId { get; set; }
        public Activity Activity { get; set; }

        public static Lazy<Activity> StaticActivity = new Lazy<Activity>(() =>
        {
            var settings = new ActivityXamlServicesSettings
            {
                CompileExpressions = true
            };

            return ActivityXamlServices.Load("MyWorkflow.xaml", settings);
        });
    }

    internal class Program
    {

        //private static ConcurrentDictionary<string, ActivityMetadata> _activities = new ConcurrentDictionary<string, ActivityMetadata>();
        static void Main(string[] args)
        {
            Console.WriteLine("Testing concurrent workflow execution with shared Activity");

            // Launch MULTIPLE threads SIMULTANEOUSLY using same cached Activity
            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 5; i++)  // 5 concurrent executions
            {
                Thread t = new Thread(RunWorkflow);
                t.Name = $"ConcurrentWorkflow{i + 1}";
                threads.Add(t);
                t.Start();  // Don't wait - let them run concurrently!
            }

            // Now wait for ALL to finish
            foreach (var t in threads)
            {
                t.Join();
            }

            Console.WriteLine("All concurrent workflows completed.");
            Console.ReadLine();
        }

        private static void RunWorkflow()
        {
            //var activity = GetOrAddActivity(xamlFileName);
            var activity = ActivityMetadata.StaticActivity.Value;


            // ALL threads create WorkflowApplication with THE SAME Activity instance
            WorkflowApplication app = new WorkflowApplication(activity);

            // Add Console.Out as a TextWriter extension so WriteLine can write to console
            app.Extensions.Add<System.IO.TextWriter>(() => Console.Out);

            using (ManualResetEvent completedEvent = new ManualResetEvent(false))
            {
                app.Completed = (e) =>
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} completed with state: {e.CompletionState}");
                    completedEvent.Set();
                };

                app.OnUnhandledException = (e) =>
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} ERROR: {e.UnhandledException.Message}");
                    completedEvent.Set();
                    return UnhandledExceptionAction.Terminate;
                };

                try
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} started");
                    app.Run();
                    completedEvent.WaitOne(); // Wait until workflow is done
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} Run() succeeded");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} EXCEPTION: {ex.Message}");
                }
            }
        }
    }
}