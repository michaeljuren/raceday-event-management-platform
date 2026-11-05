using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Participant.Controllers;

[Area("Participant")]
[Authorize(Roles = AppRoles.Participant)]
public class HomeController(IRaceDayApi api) : Controller
{
    /// <summary>Upcoming events with search, discipline, province and date filters.</summary>
    public async Task<IActionResult> Index([FromQuery] EventFilterViewModel filter, CancellationToken ct)
    {
        // Sequential on purpose: both calls may touch the session (token refresh), which is not thread-safe.
        var result = await api.GetEventsAsync(filter.ToQuery(), ct);
        var entries = await api.GetMyEntriesAsync(ct);
        ViewData["ActiveEntries"] = entries.Value?
            .Where(e => e.Status is "Pending" or "Confirmed" && e.EventDate >= Display.Today)
            .OrderBy(e => e.EventDate)
            .ToList();

        return View(new EventListViewModel
        {
            Filter = filter,
            Events = result.Value ?? new([], 1, 12, 0, 0),
            Error = result.Succeeded ? null : result.Message
        });
    }
}
