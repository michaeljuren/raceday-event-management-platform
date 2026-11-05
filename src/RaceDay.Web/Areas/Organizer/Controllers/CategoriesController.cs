using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Areas.Organizer.Models;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Organizer.Controllers;

/// <summary>Distance and age categories for an event.</summary>
[Area("Organizer")]
[Authorize(Roles = AppRoles.OrganizerOrAdmin)]
public class CategoriesController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Index(int eventId, CancellationToken ct)
    {
        var evt = await LoadManageableAsync(eventId, ct);
        return evt is null
            ? NotFound()
            : View(new CategoriesPageViewModel { Event = evt, NewCategory = new CategoryFormViewModel { EventId = eventId } });
    }

    [HttpPost]
    public async Task<IActionResult> Create(int eventId, [Bind(Prefix = "NewCategory")] CategoryFormViewModel model, CancellationToken ct)
    {
        var evt = await LoadManageableAsync(eventId, ct);
        if (evt is null)
        {
            return NotFound();
        }

        model.EventId = eventId;
        if (ModelState.IsValid)
        {
            var created = await api.CreateCategoryAsync(eventId, model.ToRequest(), ct);
            if (created.Succeeded)
            {
                this.Success($"Category \"{model.CategoryName}\" added.");
                return RedirectToAction(nameof(Index), new { eventId });
            }

            created.AddTo<CategoryFormViewModel>(ModelState, prefix: "NewCategory.");
        }

        return View(nameof(Index), new CategoriesPageViewModel { Event = evt, NewCategory = model });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var category = await api.GetCategoryAsync(id, ct);
        if (category.Value is null || await LoadManageableAsync(category.Value.EventId, ct) is null)
        {
            return NotFound();
        }

        return View(CategoryFormViewModel.From(category.Value));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model, CancellationToken ct)
    {
        var existing = await api.GetCategoryAsync(id, ct);
        if (existing.Value is null)
        {
            return NotFound();
        }

        model.CategoryId = id;
        model.EventId = existing.Value.EventId;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Keep the category's route link; the form does not edit it.
        var updated = await api.UpdateCategoryAsync(id, model.ToRequest(existing.Value.RouteId), ct);
        if (!updated.Succeeded)
        {
            updated.AddTo<CategoryFormViewModel>(ModelState);
            return View(model);
        }

        this.Success($"Category \"{model.CategoryName}\" updated.");
        return RedirectToAction(nameof(Index), new { eventId = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, int eventId, CancellationToken ct)
    {
        var result = await api.DeleteCategoryAsync(id, ct);
        if (result.Succeeded)
        {
            this.Success("Category removed.");
        }
        else
        {
            this.Error(result.Message);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    private async Task<EventDetail?> LoadManageableAsync(int eventId, CancellationToken ct)
    {
        var evt = (await api.GetEventAsync(eventId, ct)).Value;
        return evt is not null && (User.IsInRole(AppRoles.Admin) || evt.OrganizerId == User.GetUserId()) ? evt : null;
    }
}
