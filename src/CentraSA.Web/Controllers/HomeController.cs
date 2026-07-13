using System.Diagnostics;
using CentraSA.Application.Insights;
using CentraSA.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

public class HomeController(IInsightService insightService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await insightService.GetDashboardAsync(cancellationToken));

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
