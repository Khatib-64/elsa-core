using Elsa.Mediator.Contracts;
using Elsa.Workflows.Core.Models;
using Elsa.Workflows.Runtime.Models;
using Elsa.Workflows.Runtime.Models.Notifications;

namespace Elsa.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is sent when the bookmarks of a workflow instance have been indexed.
/// </summary>
/// <param name="WorkflowExecutionContext">The workflow execution context.</param>
/// <param name="IndexedWorkflowBookmarks">The bookmarks that were added, removed, or unchanged.</param>
public record WorkflowBookmarksIndexed(WorkflowExecutionContext WorkflowExecutionContext, IndexedWorkflowBookmarks IndexedWorkflowBookmarks) : INotification;