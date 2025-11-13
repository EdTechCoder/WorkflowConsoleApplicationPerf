using System;
using System.Activities;
using System.Activities.Statements;
using System.Threading;
using System.Threading.Tasks;

namespace WorkflowStateIsolationTest
{
    /// <summary>
    /// Simple workflow that maintains its own state
    /// </summary>
    public class CounterWorkflow : Activity
    {
        public InArgument<int> InstanceId { get; set; }
        public InArgument<int> StartValue { get; set; }

        public CounterWorkflow()
        {
            Implementation = () =>
            {
                // Each workflow instance has its own counter variable
                var counter = new Variable<int>("counter");

                return new Sequence
                {
                    Variables = { counter },
                    Activities =
                    {
                        // Initialize counter with start value
                        new Assign<int>
                        {
                            To = counter,
                            Value = new InArgument<int>(ctx => StartValue.Get(ctx))
                        },
                        new WriteLine
                        {
                            Text = new InArgument<string>(ctx =>
                                $"[Instance {InstanceId.Get(ctx)}] Starting with value: {counter.Get(ctx)}")
                        },
                        // Increment counter 3 times
                        new Assign<int>
                        {
                            To = counter,
                            Value = new InArgument<int>(ctx => counter.Get(ctx) + 1)
                        },
                        new Delay { Duration = TimeSpan.FromMilliseconds(100) },
                        new WriteLine
                        {
                            Text = new InArgument<string>(ctx =>
                                $"[Instance {InstanceId.Get(ctx)}] After increment 1: {counter.Get(ctx)}")
                        },
                        new Assign<int>
                        {
                            To = counter,
                            Value = new InArgument<int>(ctx => counter.Get(ctx) + 1)
                        },
                        new Delay { Duration = TimeSpan.FromMilliseconds(100) },
                        new WriteLine
                        {
                            Text = new InArgument<string>(ctx =>
                                $"[Instance {InstanceId.Get(ctx)}] After increment 2: {counter.Get(ctx)}")
                        },
                        new Assign<int>
                        {
                            To = counter,
                            Value = new InArgument<int>(ctx => counter.Get(ctx) + 1)
                        },
                        new Delay { Duration = TimeSpan.FromMilliseconds(100) },
                        new WriteLine
                        {
                            Text = new InArgument<string>(ctx =>
                                $"[Instance {InstanceId.Get(ctx)}] Final value: {counter.Get(ctx)}")
                        }
                    }
                };
            };
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Workflow State Isolation Test ===");
            Console.WriteLine("Testing that multiple instances don't interfere with each other's state\n");

            // Create 5 workflow instances with different starting values
            var tasks = new Task[5];

            for (int i = 0; i < 5; i++)
            {
                int instanceId = i + 1;
                int startValue = instanceId * 10; // 10, 20, 30, 40, 50

                tasks[i] = Task.Run(() =>
                {
                    var completionEvent = new AutoResetEvent(false);
                    Exception workflowError = null;

                    // Create workflow instance
                    var workflow = new WorkflowApplication(new CounterWorkflow
                    {
                        InstanceId = instanceId,
                        StartValue = startValue
                    });

                    // Set up completion handler
                    workflow.Completed = completedArgs =>
                    {
                        Console.WriteLine($"[Instance {instanceId}] ✓ Completed successfully");
                        completionEvent.Set();
                    };

                    workflow.Aborted = abortedArgs =>
                    {
                        workflowError = new Exception($"Aborted: {abortedArgs.Reason}");
                        completionEvent.Set();
                    };

                    workflow.OnUnhandledException = exceptionArgs =>
                    {
                        workflowError = exceptionArgs.UnhandledException;
                        return UnhandledExceptionAction.Terminate;
                    };

                    // Run the workflow
                    Console.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Starting Instance {instanceId}");
                    workflow.Run();

                    // Wait for completion
                    completionEvent.WaitOne();

                    if (workflowError != null)
                    {
                        Console.WriteLine($"[Instance {instanceId}] ✗ Error: {workflowError.Message}");
                    }
                });
            }

            // Wait for all workflows to complete
            Task.WaitAll(tasks);

            Console.WriteLine("\n=== Test Complete ===");
            Console.WriteLine("Expected results:");
            Console.WriteLine("  Instance 1: 10 → 11 → 12 → 13");
            Console.WriteLine("  Instance 2: 20 → 21 → 22 → 23");
            Console.WriteLine("  Instance 3: 30 → 31 → 32 → 33");
            Console.WriteLine("  Instance 4: 40 → 41 → 42 → 43");
            Console.WriteLine("  Instance 5: 50 → 51 → 52 → 53");
            Console.WriteLine("\nIf each instance maintained its own state correctly,");
            Console.WriteLine("the numbers above should match the output.");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}