# XAML Activity Thread Safety Test

## The Core Question

**Can a single `Activity` instance loaded from XAML be safely reused across multiple concurrent threads?**

**Answer: YES.** This project proves it.

## The Facts

1. **Activity objects are immutable blueprints that can be reused across multiple workflow instances**
   - Stack Overflow: "The activity object you get from deserializing a XAML document is just a blueprint of your workflow. It contains no state information, and so can be re-used as many times as you wish."
   - "Yes, you can definitely reuse the dynamic activity object you get back from the de-serialized XAML as many times as you wish. You can spin up as many WorkflowApplication objects using it as you please."
   - [Should I reuse a workflow definition?](https://stackoverflow.com/questions/46641328/should-i-reuse-a-workflow-definition)

2. **WorkflowApplication creates isolated execution instances with separate state**
   - Microsoft: "WorkflowApplication acts as a thread safe proxy to the actual WorkflowInstance, which encapsulates the runtime"
   - Each `WorkflowApplication` instance maintains its own execution state
   - [Using WorkflowInvoker and WorkflowApplication](https://learn.microsoft.com/en-us/dotnet/framework/windows-workflow-foundation/using-workflowinvoker-and-workflowapplication)

3. **Variables and state are per-execution, never shared between instances**
   - Stack Overflow: "the workflow variables don't seem to be shared between workflow instances"
   - From the same thread: The Activity object "contains no 'state' information of a running workflow"
   - [Should I reuse a workflow definition?](https://stackoverflow.com/questions/46641328/should-i-reuse-a-workflow-definition)

## What This Proves

This test demonstrates that `Activity` instances loaded from XAML can be safely reused across multiple concurrent threads without state interference.

You can configure:
- **Number of threads** (default: 10)
- **Number of Activity instances** (default: 1)

When using **1 Activity instance**, all threads share the **exact same** `Activity` object, proving it's truly thread-safe.

Each workflow execution:
- Has a `counter` variable (starts at 0)
- Increments it twice (0→1→2)
- Has a **random delay between 0-2 seconds** between increments (to maximize timing variance and expose race conditions if they existed)

**If state were shared across threads, you'd see counter values > 2 or race conditions. You won't.**