using CentraSA.Application.Smuds;

namespace CentraSA.Web.ViewModels.Smuds;

public sealed class SmudIndexViewModel
{
    public string? Search { get; set; }

    public Guid? AreaId { get; set; }

    public Guid? PersonId { get; set; }

    public Guid? StatusId { get; set; }

    public SmudDueFilter DueFilter { get; set; }

    public bool ActionRequiredOnly { get; set; }

    public bool HideFinalized { get; set; } = true;

    public SmudBoardData Data { get; set; } = null!;
}
