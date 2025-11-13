# XAML Activity Thread Safety Test

## The Facts

1. **Activity objects are blueprints that can be reused across multiple workflow instances**
   - Stack Overflow: "The activity object you get from deserializing a XAML document is just a blueprint of your workflow. It contains no state information, and so can be re-used as many times as you wish."
   - "Yes, you can definitely reuse the dynamic activity object you get back from the de-serialized XAML as many times as you wish. You can spin up as many WorkflowApplication objects using it as you please."
   - [Should I reuse a workflow definition?](https://stackoverflow.com/questions/46641328/should-i-reuse-a-workflow-definition)

2. **WorkflowApplication creates isolated execution instances with separate state**
   - Microsoft: "WorkflowApplication acts as a thread safe proxy to the actual WorkflowInstance, which encapsulates the runtime"
   - [Using WorkflowInvoker and WorkflowApplication](https://learn.microsoft.com/en-us/dotnet/framework/windows-workflow-foundation/using-workflowinvoker-and-workflowapplication)

3. **Variables and state are per-execution, never shared between instances**
   - Stack Overflow: "the workflow variables don't seem to be shared between workflow instances"
   - From the same thread: The Activity object "contains no 'state' information of a running workflow"
   - [Should I reuse a workflow definition?](https://stackoverflow.com/questions/46641328/should-i-reuse-a-workflow-definition)

## What This Does

Loads Activity instances from XAML and executes them concurrently across multiple threads to prove state is not shared.

Each workflow:
- Has a `counter` variable (starts at 0)
- Increments it twice (0→1→2)
- Has a delay between increments (to expose potential race conditions)

If state were shared, you'd see counter values > 2. You won't.

## Run It

```bash
dotnet new console
dotnet add package System.Activities
# Copy Program.cs code
dotnet run
```

Enter thread count and workflow instance count, or press Enter for defaults (10 threads, 5 instances).

## Expected Output

```
Configuration: 10 threads, 5 workflow(s)

Thread 0 starting (using Activity 0)...
Thread 1 starting (using Activity 1)...
Counter value: 1
Final counter: 2
Counter value: 1
Final counter: 2
...
Thread 0 completed.

✓ All threads completed successfully!
✓ Each thread had isolated state (counter always: 1, then 2).
```

Every execution shows counter values of 1, then 2. Never 3, 4, 5, etc.

## Why This Works

- **Activity** = immutable workflow definition (shareable)
- **WorkflowApplication** = runtime execution context (isolated per thread)

Multiple WorkflowApplications can safely use the same Activity.