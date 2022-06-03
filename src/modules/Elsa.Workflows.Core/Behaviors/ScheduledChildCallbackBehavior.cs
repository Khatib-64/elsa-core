using Elsa.Workflows.Core.Models;
using Elsa.Workflows.Core.Services;
using Elsa.Workflows.Core.Signals;

namespace Elsa.Workflows.Core.Behaviors;

/// <summary>
/// Implements a behavior that invokes "child completed" callbacks on parent activities.
/// </summary>
public class ScheduledChildCallbackBehavior : Behavior
{
    public ScheduledChildCallbackBehavior(IActivity owner) : base(owner)
    {
        OnSignalReceived<ActivityCompleted>(OnActivityCompletedAsync);
    }

    private async ValueTask OnActivityCompletedAsync(ActivityCompleted signal, SignalContext context)
    {
        var activityExecutionContext = context.ActivityExecutionContext;
        var childActivityExecutionContext = context.SourceActivityExecutionContext;
        var childActivity = childActivityExecutionContext.Activity;
        var callbackEntry = activityExecutionContext.WorkflowExecutionContext.PopCompletionCallback(activityExecutionContext, childActivity);

        if (callbackEntry == null)
            return;

        await callbackEntry(activityExecutionContext, childActivityExecutionContext);
    }
}