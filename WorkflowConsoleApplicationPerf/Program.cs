using System;
using System.Activities;
using System.Activities.XamlIntegration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xaml;

namespace WorkflowStateIsolationTest
{
    public class Program
    {
        private static ConcurrentDictionary<int, List<int>> _workflowResults = new ConcurrentDictionary<int, List<int>>();
        private static object _consoleLock = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("=== Workflow State Isolation Test (XAML) ===");
            Console.WriteLine("Testing that multiple instances don't interfere with each other's state\n");

            // Get the number of workflow instances to run
            Console.Write("Enter number of workflow instances to run (default 5): ");
            string input = Console.ReadLine();
            int workflowCount = 5;
            if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int parsed) && parsed > 0)
            {
                workflowCount = parsed;
            }

            // Get the number of threads to use
            Console.Write("Enter number of threads (default = workflow count): ");
            input = Console.ReadLine();
            int threadCount = workflowCount;
            if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out parsed) && parsed > 0)
            {
                threadCount = parsed;
            }

            Console.WriteLine($"\nRunning {workflowCount} workflow instances across {threadCount} threads...\n");

            // Create the XAML workflow file
            CreateWorkflowXaml();

            // Create workflow instances with different starting values
            var tasks = new Task[workflowCount];

            for (int i = 0; i < workflowCount; i++)
            {
                int instanceId = i + 1;
                int startValue = instanceId * 10; // 10, 20, 30, 40, 50

                tasks[i] = Task.Run(() =>
                {
                    var completionEvent = new AutoResetEvent(false);
                    Exception workflowError = null;

                    // Initialize results tracking for this instance
                    _workflowResults[instanceId] = new List<int>();

                    // Load workflow from XAML
                    Activity workflow = LoadWorkflowFromXaml("CounterWorkflow.xaml");

                    // Create workflow application with input arguments
                    var workflowApp = new WorkflowApplication(workflow, new Dictionary<string, object>
                    {
                        { "InstanceId", instanceId },
                        { "StartValue", startValue }
                    });

                    // Set up completion handler
                    workflowApp.Completed = completedArgs =>
                    {
                        lock (_consoleLock)
                        {
                            Console.WriteLine($"[Instance {instanceId}] ✓ Completed successfully");
                        }
                        completionEvent.Set();
                    };

                    workflowApp.Aborted = abortedArgs =>
                    {
                        workflowError = new Exception($"Aborted: {abortedArgs.Reason}");
                        completionEvent.Set();
                    };

                    workflowApp.OnUnhandledException = exceptionArgs =>
                    {
                        workflowError = exceptionArgs.UnhandledException;
                        return UnhandledExceptionAction.Terminate;
                    };

                    // Run the workflow
                    lock (_consoleLock)
                    {
                        Console.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Starting Instance {instanceId}");
                    }
                    workflowApp.Run();

                    // Wait for completion
                    completionEvent.WaitOne();

                    if (workflowError != null)
                    {
                        lock (_consoleLock)
                        {
                            Console.WriteLine($"[Instance {instanceId}] ✗ Error: {workflowError.Message}");
                        }
                    }
                });
            }

            // Wait for all workflows to complete
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = threadCount };
            Parallel.ForEach(tasks, parallelOptions, task => task.Wait());

            Console.WriteLine("\n=== Validation Results ===");

            // Validate all workflows
            int passedCount = 0;
            int failedCount = 0;

            for (int i = 1; i <= workflowCount; i++)
            {
                int startValue = i * 10;
                var expected = new List<int> { startValue, startValue + 1, startValue + 2, startValue + 3 };

                if (_workflowResults.TryGetValue(i, out var actual))
                {
                    bool isValid = expected.SequenceEqual(actual);

                    if (isValid)
                    {
                        Console.WriteLine($"✓ Instance {i}: PASS - {string.Join(" → ", actual)}");
                        passedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"✗ Instance {i}: FAIL");
                        Console.WriteLine($"  Expected: {string.Join(" → ", expected)}");
                        Console.WriteLine($"  Actual:   {string.Join(" → ", actual)}");
                        failedCount++;
                    }
                }
                else
                {
                    Console.WriteLine($"✗ Instance {i}: FAIL - No results captured");
                    failedCount++;
                }
            }

            Console.WriteLine($"\n=== Summary ===");
            Console.WriteLine($"Total Workflows: {workflowCount}");
            Console.WriteLine($"Threads Used: {threadCount}");
            Console.WriteLine($"Passed: {passedCount}");
            Console.WriteLine($"Failed: {failedCount}");

            if (failedCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ ALL TESTS PASSED - State isolation is working correctly!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ {failedCount} TESTS FAILED - State isolation may be compromised!");
                Console.ResetColor();
            }

            Console.WriteLine("\nIf each instance maintained its own state correctly,");
            Console.WriteLine("the numbers above should match the output.");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void CreateWorkflowXaml()
        {
            string xaml = @"<Activity x:Class=""CounterWorkflow""
 xmlns=""http://schemas.microsoft.com/netfx/2009/xaml/activities""
 xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
 xmlns:s=""clr-namespace:System;assembly=mscorlib""
 xmlns:sa=""clr-namespace:System.Activities;assembly=System.Activities""
 xmlns:local=""clr-namespace:WorkflowStateIsolationTest;assembly=WorkflowConsoleApplicationPerf"">
  <x:Members>
    <x:Property Name=""InstanceId"" Type=""InArgument(x:Int32)"" />
    <x:Property Name=""StartValue"" Type=""InArgument(x:Int32)"" />
  </x:Members>
  <Sequence>
    <Sequence.Variables>
      <Variable x:TypeArguments=""x:Int32"" Name=""counter"" />
    </Sequence.Variables>
    
    <!-- Initialize counter -->
    <Assign x:TypeArguments=""x:Int32"">
      <Assign.To>
        <OutArgument x:TypeArguments=""x:Int32"">[counter]</OutArgument>
      </Assign.To>
      <Assign.Value>
        <InArgument x:TypeArguments=""x:Int32"">[StartValue]</InArgument>
      </Assign.Value>
    </Assign>
    
    <WriteLine>
      <InArgument x:TypeArguments=""x:String"">
        [""[Instance "" + InstanceId.ToString() + ""] Starting with value: "" + counter.ToString()]
      </InArgument>
    </WriteLine>
    
    <!-- Track starting value -->
    <InvokeMethod MethodName=""TrackValue"" TargetType=""local:Program"">
      <InArgument x:TypeArguments=""x:Int32"">[InstanceId]</InArgument>
      <InArgument x:TypeArguments=""x:Int32"">[counter]</InArgument>
    </InvokeMethod>
    
    <!-- Increment 1 -->
    <Assign x:TypeArguments=""x:Int32"">
      <Assign.To>
        <OutArgument x:TypeArguments=""x:Int32"">[counter]</OutArgument>
      </Assign.To>
      <Assign.Value>
        <InArgument x:TypeArguments=""x:Int32"">[counter + 1]</InArgument>
      </Assign.Value>
    </Assign>
    
    <Delay Duration=""00:00:00.100"" />
    
    <WriteLine>
      <InArgument x:TypeArguments=""x:String"">
        [""[Instance "" + InstanceId.ToString() + ""] After increment 1: "" + counter.ToString()]
      </InArgument>
    </WriteLine>
    
    <InvokeMethod MethodName=""TrackValue"" TargetType=""local:Program"">
      <InArgument x:TypeArguments=""x:Int32"">[InstanceId]</InArgument>
      <InArgument x:TypeArguments=""x:Int32"">[counter]</InArgument>
    </InvokeMethod>
    
    <!-- Increment 2 -->
    <Assign x:TypeArguments=""x:Int32"">
      <Assign.To>
        <OutArgument x:TypeArguments=""x:Int32"">[counter]</OutArgument>
      </Assign.To>
      <Assign.Value>
        <InArgument x:TypeArguments=""x:Int32"">[counter + 1]</InArgument>
      </Assign.Value>
    </Assign>
    
    <Delay Duration=""00:00:00.100"" />
    
    <WriteLine>
      <InArgument x:TypeArguments=""x:String"">
        [""[Instance "" + InstanceId.ToString() + ""] After increment 2: "" + counter.ToString()]
      </InArgument>
    </WriteLine>
    
    <InvokeMethod MethodName=""TrackValue"" TargetType=""local:Program"">
      <InArgument x:TypeArguments=""x:Int32"">[InstanceId]</InArgument>
      <InArgument x:TypeArguments=""x:Int32"">[counter]</InArgument>
    </InvokeMethod>
    
    <!-- Increment 3 -->
    <Assign x:TypeArguments=""x:Int32"">
      <Assign.To>
        <OutArgument x:TypeArguments=""x:Int32"">[counter]</OutArgument>
      </Assign.To>
      <Assign.Value>
        <InArgument x:TypeArguments=""x:Int32"">[counter + 1]</InArgument>
      </Assign.Value>
    </Assign>
    
    <Delay Duration=""00:00:00.100"" />
    
    <WriteLine>
      <InArgument x:TypeArguments=""x:String"">
        [""[Instance "" + InstanceId.ToString() + ""] Final value: "" + counter.ToString()]
      </InArgument>
    </WriteLine>
    
    <InvokeMethod MethodName=""TrackValue"" TargetType=""local:Program"">
      <InArgument x:TypeArguments=""x:Int32"">[InstanceId]</InArgument>
      <InArgument x:TypeArguments=""x:Int32"">[counter]</InArgument>
    </InvokeMethod>
  </Sequence>
</Activity>";

            File.WriteAllText("CounterWorkflow.xaml", xaml);
            Console.WriteLine("Created CounterWorkflow.xaml\n");
        }

        public static void TrackValue(int instanceId, int value)
        {
            _workflowResults.AddOrUpdate(instanceId,
                new List<int> { value },
                (key, list) => { list.Add(value); return list; });
        }

        static Activity LoadWorkflowFromXaml(string xamlPath)
        {
            using (var stream = File.OpenRead(xamlPath))
            {
                return ActivityXamlServices.Load(stream);
            }
        }
    }
}