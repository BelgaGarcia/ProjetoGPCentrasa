using CentraSA.Application.Insights;
using CentraSA.Web.ViewModels.Insights;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("painel")]
public sealed class DashboardController(IInsightService insightService) : Controller
{
    [HttpGet("itens")]
    public async Task<IActionResult> Items(
        [FromQuery] DashboardItemsViewModel model,
        CancellationToken cancellationToken)
    {
        model.Data = await insightService.SearchItemsAsync(
            model.Search,
            model.SourceType,
            model.State,
            cancellationToken);
        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return PartialView("_OperationalList", model);
        }

        return View(model);
    }
}
