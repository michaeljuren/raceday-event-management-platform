using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Participant.Controllers;

/// <summary>Event detail with the enrol form. The API decides whether an entry is allowed.</summary>
[Area("Participant")]
[Authorize(Roles = AppRoles.Participant)]
public class EventsController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await BuildAsync(id, null, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Enrol(int id, int? categoryId, CancellationToken ct)
    {
        if (categoryId is null)
        {
            ModelState.AddModelError("categoryId", "Choose a category to enter.");
        }
        else
        {
            var result = await api.EnterAsync(categoryId.Value, ct);
            if (result.Succeeded && result.Value is not null)
            {
                this.Success(result.Value.Status == "Pending"
                    ? $"You're entered in {result.Value.CategoryName}! Your entry is pending until payment is confirmed."
                    : $"You're entered in {result.Value.CategoryName}. See you on the start line!");
                return RedirectToAction("Index", "Enrolments");
            }

            // Closed registration, full category, age/gender rules or a duplicate entry all come back from the API.
            ModelState.AddModelError("categoryId", result.Message);
        }

        var model = await BuildAsync(id, categoryId, ct);
        return model is null ? NotFound() : View(nameof(Details), model);
    }

    private async Task<EventEntryViewModel?> BuildAsync(int id, int? selected, CancellationToken ct)
    {
        var evt = (await api.GetEventAsync(id, ct)).Value;
        if (evt is null)
        {
            return null;
        }

        var mine = (await api.GetMyEntriesAsync(ct)).Value ?? [];
        var current = mine.FirstOrDefault(e => e.EventId == id && e.Status is "Pending" or "Confirmed");
        return new EventEntryViewModel(evt, current, selected, evt.OrganizerId == User.GetUserId());
    }
}

public record EventEntryViewModel(EventDetail Event, Entry? MyEntry, int? SelectedCategoryId, bool IsOwnEvent)
{
    public bool RegistrationOpen =>
        Event.Status == "Published" && Display.RegistrationOpen(Event.RegistrationOpenDate, Event.RegistrationCloseDate);
}
