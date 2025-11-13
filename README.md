# XAML Activity Thread Safety Test

## The Facts

1. **Activity instances loaded from XAML are thread-safe templates** - They can be reused across multiple threads
   - [Activity Class Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.activities.activity)
   - [Thread Safety in Workflow Foundation](https://learn.microsoft.com/en-us/dotnet/framework/windows-workflow-foundation/instance-stores)

2. **WorkflowApplication creates isolated execution state** - Each execution gets its own variables and state
   - [WorkflowApplication Class](https://learn.microsoft.com/en-us/dotnet/api/system.activities.workflowapplication)
   - [Workflow Execution Model](https://learn.microsoft.com/en-us/dotnet/framework/windows-workflow-foundation/overview)

3. **No state is shared between executions** - Even when using the same Activity instance
   - [Variable and Argument Best Practices](https://learn.microsoft.com/en-us/dotnet/framework/windows-workflow-foundation/variable-and-argument-tracking)

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