using CentraSA.Application.Insights;
using CentraSA.Domain.Enums;
using CentraSA.Web.ViewModels.Insights;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("historico")]
public sealed class HistoryController(IInsightService insightService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] HistoryIndexViewModel model,
        CancellationToken cancellationToken)
    {
        model.Data = await insightService.SearchHistoryAsync(
            new GlobalHistorySearch(
                model.Search,
                model.EntityType,
                model.ActionType,
                model.FromDate,
                model.ToDate,
                model.Page),
            cancellationToken);
        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return PartialView("_Timeline", model);
        }

        return View(model);
    }

    [HttpGet("{entityType}/{id:guid}")]
    public async Task<IActionResult> Details(
        TrackedEntityType entityType,
        Guid id,
        CancellationToken cancellationToken)
    {
        HistoryDetailsData? data = await insightService.GetHistoryDetailsAsync(entityType, id, cancellationToken);
        return data is null ? NotFound() : View(data);
    }
}
