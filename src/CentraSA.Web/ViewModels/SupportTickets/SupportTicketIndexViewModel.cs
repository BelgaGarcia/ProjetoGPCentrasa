using CentraSA.Application.SupportTickets;

namespace CentraSA.Web.ViewModels.SupportTickets;

public sealed class SupportTicketIndexViewModel
{
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? AreaId { get; set; }

    public Guid? PersonId { get; set; }

    public Guid? StatusId { get; set; }

    public SupportTicketDueFilter DueFilter { get; set; }

    public bool ActionRequiredOnly { get; set; }

    public bool HideFinalized { get; set; } = true;

    public SupportTicketBoardData Data { get; set; } = null!;
}
