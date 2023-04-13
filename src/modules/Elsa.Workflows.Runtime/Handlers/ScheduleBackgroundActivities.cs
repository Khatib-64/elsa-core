using Elsa.Extensions;
using Elsa.Mediator.Contracts;
using Elsa.Workflows.Core.Contracts;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Middleware.Activities;
using Elsa.Workflows.Runtime.Middleware.Workflows;
using Elsa.Workflows.Runtime.Models;
using Elsa.Workflows.Runtime.Models.Bookmarks;
using Elsa.Workflows.Runtime.Notifications;
using JetBrains.Annotations;

namespace Elsa.Workflows.Runtime.Handlers;

/// <summary>
/// A handler that schedules background activities.
/// </summary>
[PublicAPI]
public class ScheduleBackgroundActivities : INotificationHandler<WorkflowBookmarksIndexed>
{
    private readonly IBackgroundActivityScheduler _backgroundActivityScheduler;
    private readonly IBookmarkPayloadSerializer _bookmarkPayloadSerializer;
    private readonly IBookmarkHasher _bookmarkHasher;
    private readonly IWorkflowRuntime _workflowRuntime;
    private IWorkflowStateSerializer _workflowStateSerializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledBackgroundActivity"/> class.
    /// </summary>
    public ScheduleBackgroundActivities(
        IBackgroundActivityScheduler backgroundActivityScheduler,
        IBookmarkPayloadSerializer bookmarkPayloadSerializer,
        IBookmarkHasher bookmarkHasher,
        IWorkflowRuntime workflowRuntime, 
        IWorkflowStateSerializer workflowStateSerializer)
    {
        _backgroundActivityScheduler = backgroundActivityScheduler;
        _bookmarkPayloadSerializer = bookmarkPayloadSerializer;
        _bookmarkHasher = bookmarkHasher;
        _workflowRuntime = workflowRuntime;
        _workflowStateSerializer = workflowStateSerializer;
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkflowBookmarksIndexed notification, CancellationToken cancellationToken)
    {
        var workflowExecutionContext = notification.WorkflowExecutionContext;

        var scheduledBackgroundActivities = workflowExecutionContext
            .TransientProperties
            .GetOrAdd(BackgroundActivityInvokerMiddleware.BackgroundActivitySchedulesKey, () => new List<ScheduledBackgroundActivity>());

        var bookmarks = notification.IndexedWorkflowBookmarks.AddedBookmarks;

        foreach (var scheduledBackgroundActivity in scheduledBackgroundActivities)
        {
            // Schedule the background activity.
            var jobId = await _backgroundActivityScheduler.ScheduleAsync(scheduledBackgroundActivity, cancellationToken);

            // Select the bookmark associated with the background activity.
            var bookmark = workflowExecutionContext.Bookmarks.First(x => x.Id == scheduledBackgroundActivity.BookmarkId);
            var payload = _bookmarkPayloadSerializer.Deserialize<BackgroundActivityBookmark>(bookmark.Data!);

            // Store the created job ID.
            workflowExecutionContext.Bookmarks.Remove(bookmark);
            payload.JobId = jobId;
            bookmark = bookmark with
            {
                Data = _bookmarkPayloadSerializer.Serialize(payload),
                Hash = _bookmarkHasher.Hash(bookmark.Name, payload)
            };
            workflowExecutionContext.Bookmarks.Add(bookmark);
            
            // Update the bookmark.
            var storedBookmark = new StoredBookmark(
                bookmark.Name,
                bookmark.Hash,
                workflowExecutionContext.Id,
                bookmark.Id,
                workflowExecutionContext.CorrelationId,
                bookmark.Data
            );
            
            await _workflowRuntime.UpdateBookmarkAsync(storedBookmark, cancellationToken);
        }

        if (scheduledBackgroundActivities.Any())
        {
            // Bookmarks got updated, so we need to update the workflow state.
            var workflowState = _workflowStateSerializer.SerializeState(workflowExecutionContext);
            await _workflowRuntime.ImportWorkflowStateAsync(workflowState, cancellationToken);
        }
    }
}