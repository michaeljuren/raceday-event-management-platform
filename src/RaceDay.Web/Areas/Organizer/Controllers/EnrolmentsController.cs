using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Organizer.Controllers;

/// <summary>The roster of participants enrolled in an event.</summary>
[Area("Organizer")]
[Authorize(Roles = AppRoles.OrganizerOrAdmin)]
public class EnrolmentsController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Index(int eventId, int? categoryId, CancellationToken ct)
    {
        var evt = (await api.GetEventAsync(eventId, ct)).Value;
        if (evt is null)
        {
            return NotFound();
        }

        var roster = await api.GetRosterAsync(eventId, ct);
        if (!roster.Succeeded)
        {
            this.Error(roster.Message);
            return RedirectToAction("Index", "Events");
        }

        return View(new RosterViewModel(evt, roster.Value ?? [], categoryId));
    }
}

public record RosterViewModel(EventDetail Event, IReadOnlyList<RosterEntry> Entries, int? CategoryId)
{
    public IEnumerable<RosterEntry> Filtered =>
        CategoryId is { } id ? Entries.Where(e => e.CategoryId == id) : Entries;
}
