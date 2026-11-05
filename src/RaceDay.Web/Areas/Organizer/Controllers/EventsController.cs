using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RaceDay.Web.Areas.Organizer.Models;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Organizer.Controllers;

/// <summary>Create, edit, publish and delete events. Every change is made through the API.</summary>
[Area("Organizer")]
[Authorize(Roles = AppRoles.OrganizerOrAdmin)]
public class EventsController(IRaceDayApi api) : Controller
{
    private static readonly string[] BannerTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxBannerBytes = 5 * 1024 * 1024;

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        // Organizers see their own events (drafts included); admins see everything.
        var mine = !User.IsInRole(AppRoles.Admin);
        var result = await api.GetEventsAsync(new EventQuery(Mine: mine, Page: Math.Max(1, page), PageSize: 20), ct);
        if (!result.Succeeded)
        {
            this.Error(result.Message);
        }

        return View(result.Value ?? new PagedResult<EventSummary>([], 1, 20, 0, 0));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var evt = await LoadManageableAsync(id, ct);
        return evt is null ? NotFound() : View(evt);
    }

    [Authorize(Roles = AppRoles.Organizer)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = EventFormViewModel.ForNew();
        await FillVenuesAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Organizer)]
    public async Task<IActionResult> Create(EventFormViewModel model, CancellationToken ct)
    {
        model.EventId = null;
        ValidateBanner(model.Banner);
        if (!ModelState.IsValid)
        {
            return await FormAsync(model, ct);
        }

        var created = await api.CreateEventAsync(model.ToRequest(), ct);
        if (!created.Succeeded || created.Value is null)
        {
            created.AddTo<EventFormViewModel>(ModelState);
            return await FormAsync(model, ct);
        }

        var eventId = created.Value.EventId;
        var problems = new List<string>();

        if (model.InitialDistanceKm is { } km)
        {
            var category = await api.CreateCategoryAsync(eventId, new CategoryRequest(
                null, $"{Display.Distance(km).Replace(" ", "")}", km, null, "Open", model.InitialEntryFee ?? 0, null), ct);
            if (!category.Succeeded)
            {
                problems.Add($"The {Display.Distance(km)} category could not be added: {category.Message}");
            }
        }

        if (model.Banner is not null && await UploadBannerAsync(eventId, model.Banner, ct) is { } bannerError)
        {
            problems.Add(bannerError);
        }

        if (problems.Count > 0)
        {
            this.Error($"The event was saved as a draft, but: {string.Join(" ", problems)}");
        }
        else
        {
            this.Success($"\"{created.Value.EventName}\" was created as a draft. Add categories, then publish it.");
        }

        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var evt = await LoadManageableAsync(id, ct);
        if (evt is null)
        {
            return NotFound();
        }

        var model = EventFormViewModel.From(evt);
        await FillVenuesAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model, CancellationToken ct)
    {
        model.EventId = id;
        ValidateBanner(model.Banner);
        if (!ModelState.IsValid)
        {
            return await FormAsync(model, ct);
        }

        var updated = await api.UpdateEventAsync(id, model.ToRequest(), ct);
        if (!updated.Succeeded)
        {
            if (updated.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden)
            {
                this.Error(updated.Message);
                return RedirectToAction(nameof(Index));
            }

            updated.AddTo<EventFormViewModel>(ModelState);
            return await FormAsync(model, ct);
        }

        if (model.Banner is not null && await UploadBannerAsync(id, model.Banner, ct) is { } bannerError)
        {
            this.Error($"The event was saved, but {bannerError}");
        }
        else
        {
            this.Success("Event updated.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var evt = await LoadManageableAsync(id, ct);
        return evt is null ? NotFound() : View(evt);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        var evt = await LoadManageableAsync(id, ct);
        if (evt is null)
        {
            return NotFound();
        }

        var result = await api.DeleteEventAsync(id, ct);
        if (!result.Succeeded)
        {
            this.Error(result.Message);
            return RedirectToAction(nameof(Details), new { id });
        }

        var hadEntries = evt.Categories.Any(c => c.EntryCount > 0);
        this.Success(hadEntries
            ? $"\"{evt.EventName}\" has entries, so it was cancelled and its entries were cancelled."
            : $"\"{evt.EventName}\" was deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        var result = await api.PublishEventAsync(id, ct);
        if (result.Succeeded)
        {
            this.Success("Event published. Participants can now find it and enter.");
        }
        else
        {
            this.Error(result.Message);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Loads an event the current user may manage (their own, or any for admins).</summary>
    private async Task<EventDetail?> LoadManageableAsync(int id, CancellationToken ct)
    {
        var result = await api.GetEventAsync(id, ct);
        var evt = result.Value;
        if (evt is null || (!User.IsInRole(AppRoles.Admin) && evt.OrganizerId != User.GetUserId()))
        {
            return null;
        }

        return evt;
    }

    private async Task<string?> UploadBannerAsync(int eventId, IFormFile banner, CancellationToken ct)
    {
        var upload = await api.UploadBannerAsync(eventId, banner, ct);
        return upload.Succeeded ? null : $"the banner could not be uploaded: {upload.Message}";
    }

    /// <summary>Quick checks before uploading; the API validates the file again.</summary>
    private void ValidateBanner(IFormFile? banner)
    {
        if (banner is null)
        {
            return;
        }

        if (banner.Length > MaxBannerBytes)
        {
            ModelState.AddModelError(nameof(EventFormViewModel.Banner), "The banner image must be 5 MB or smaller.");
        }
        else if (!BannerTypes.Contains(banner.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(EventFormViewModel.Banner), "The banner must be a JPEG, PNG or WebP image.");
        }
    }

    private async Task<IActionResult> FormAsync(EventFormViewModel model, CancellationToken ct)
    {
        await FillVenuesAsync(model, ct);
        return View("Form", model);
    }

    private async Task FillVenuesAsync(EventFormViewModel model, CancellationToken ct)
    {
        var venues = await api.GetVenuesAsync(ct);
        model.Venues = (venues.Value ?? [])
            .OrderBy(v => v.Province).ThenBy(v => v.VenueName)
            .Select(v => new SelectListItem($"{v.VenueName} — {v.City}, {v.Province}", v.VenueId.ToString()))
            .ToList();
    }
}
