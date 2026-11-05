using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Organizer.Controllers;

[Area("Organizer")]
[Authorize(Roles = AppRoles.OrganizerOrAdmin)]
public class DashboardController(IRaceDayApi api) : Controller
{
    /// <summary>Summary cards plus every event with its enrolment totals.</summary>
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await api.GetDashboardAsync(ct);
        if (!result.Succeeded)
        {
            this.Error(result.Message);
        }

        return View(result.Value ?? []);
    }
}
