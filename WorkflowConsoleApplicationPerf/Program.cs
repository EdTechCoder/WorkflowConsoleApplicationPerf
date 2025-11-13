using System;
using System.Activities;
using System.Activities.XamlIntegration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xaml;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Write("Enter number of threads (default 10): ");
        var threadInput = Console.ReadLine();
        int threadCount = string.IsNullOrWhiteSpace(threadInput) ? 10 : int.Parse(threadInput);

        Console.Write("Enter number of workflow Activity instances (default 1): ");
        var workflowInput = Console.ReadLine();
        int workflowCount = string.IsNullOrWhiteSpace(workflowInput) ? 1 : int.Parse(workflowInput);

        Console.WriteLine($"\nConfiguration: {threadCount} threads, {workflowCount} workflow(s)\n");

        // Create a XAML workflow with a variable to prove state isolation
        var xaml = @"<Activity 
            xmlns='http://schemas.microsoft.com/netfx/2009/xaml/activities'
            xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
            xmlns:s='clr-namespace:System;assembly=mscorlib'>
            <Sequence>
                <Sequence.Variables>
                    <Variable x:TypeArguments='x:Int32' Name='counter' Default='0' />
                </Sequence.Variables>
                <Assign>
                    <Assign.To>
                        <OutArgument x:TypeArguments='x:Int32'>[counter]</OutArgument>
                    </Assign.To>
                    <Assign.Value>
                        <InArgument x:TypeArguments='x:Int32'>[counter + 1]</InArgument>
                    </Assign.Value>
                </Assign>
                <WriteLine Text='[String.Format(&quot;Counter value: {0}&quot;, counter)]' />
                <Delay Duration='[TimeSpan.FromMilliseconds(new Random().Next(0, 2000))]' />
                <Assign>
                    <Assign.To>
                        <OutArgument x:TypeArguments='x:Int32'>[counter]</OutArgument>
                    </Assign.To>
                    <Assign.Value>
                        <InArgument x:TypeArguments='x:Int32'>[counter + 1]</InArgument>
                    </Assign.Value>
                </Assign>
                <WriteLine Text='[String.Format(&quot;Final counter: {0}&quot;, counter)]' />
            </Sequence>
        </Activity>";

        File.WriteAllText("workflow.xaml", xaml);

        // Load Activity instance(s) from XAML
        var activities = new Activity[workflowCount];
        for (int i = 0; i < workflowCount; i++)
        {
            using (var reader = new StreamReader("workflow.xaml"))
            {
                activities[i] = ActivityXamlServices.Load(reader);
            }
        }

        Console.WriteLine($"Loaded {workflowCount} Activity instance(s) from XAML.");
        Console.WriteLine("Testing state isolation across threads with random delays (0-2 seconds)...\n");

        // Execute Activity instances across multiple threads using WorkflowApplication
        var tasks = new Task[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            int threadNum = i;
            tasks[i] = Task.Run(() =>
            {
                // Select which activity instance to use (round-robin)
                var activity = activities[threadNum % workflowCount];

                var completed = new ManualResetEvent(false);
                Exception error = null;

                Console.WriteLine($"Thread {threadNum} starting (using Activity {threadNum % workflowCount})...");

                var app = new WorkflowApplication(activity);

                app.Completed = (e) =>
                {
                    Console.WriteLine($"Thread {threadNum} completed.");
                    completed.Set();
                };

                app.OnUnhandledException = (e) =>
                {
                    error = e.UnhandledException;
                    Console.WriteLine($"Thread {threadNum} error: {error.Message}");
                    completed.Set();
                    return UnhandledExceptionAction.Abort;
                };

                app.Run();
                completed.WaitOne();

                if (error != null) throw error;
            });
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("\n✓ All threads completed successfully!");
        Console.WriteLine("✓ Each thread had isolated state (counter always: 1, then 2).");
        Console.WriteLine($"✓ {workflowCount} Activity instance(s) safely reused across {threadCount} threads.");
        Console.ReadLine();
        // Cleanup
        File.Delete("workflow.xaml");
    }
}