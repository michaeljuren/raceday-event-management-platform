using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Participant.Controllers;

/// <summary>The participant's performance history: every result recorded against their entries.</summary>
[Area("Participant")]
[Authorize(Roles = AppRoles.Participant)]
public class ResultsController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await api.GetPerformanceHistoryAsync(ct);
        if (!result.Succeeded)
        {
            this.Error(result.Message);
        }

        return View(result.Value ?? []);
    }
}
