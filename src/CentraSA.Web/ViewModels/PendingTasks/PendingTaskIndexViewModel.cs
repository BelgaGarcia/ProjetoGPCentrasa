using CentraSA.Application.PendingTasks;

namespace CentraSA.Web.ViewModels.PendingTasks;

public sealed class PendingTaskIndexViewModel
{
    public string? Search { get; set; }

    public Guid? AreaId { get; set; }

    public Guid? PersonId { get; set; }

    public Guid? StatusId { get; set; }

    public PendingTaskDueFilter DueFilter { get; set; }

    public bool HideCompleted { get; set; } = true;

    public int Page { get; set; } = 1;

    public PendingTaskListData Data { get; set; } = null!;
}
