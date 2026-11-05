using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Controllers;

/// <summary>Public event calendar, open to everyone including anonymous visitors.</summary>
[AllowAnonymous]
public class EventsController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Index([FromQuery] EventFilterViewModel filter, CancellationToken ct)
    {
        if (User.IsParticipant())
        {
            return RedirectToAction("Index", "Home", new { area = "Participant", filter.Search, filter.Discipline, filter.Province, filter.FromDate, filter.ToDate });
        }

        var result = await api.GetEventsAsync(filter.ToQuery(), ct);
        return View(new EventListViewModel
        {
            Filter = filter,
            Events = result.Value ?? new([], 1, 12, 0, 0),
            Error = result.Succeeded ? null : result.Message
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        if (User.IsParticipant())
        {
            return RedirectToAction("Details", "Events", new { area = "Participant", id });
        }

        var result = await api.GetEventAsync(id, ct);
        return result.Value is null ? NotFound() : View(result.Value);
    }
}
