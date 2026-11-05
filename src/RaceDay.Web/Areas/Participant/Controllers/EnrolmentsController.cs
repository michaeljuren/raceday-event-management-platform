using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Participant.Controllers;

[Area("Participant")]
[Authorize(Roles = AppRoles.Participant)]
public class EnrolmentsController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await api.GetMyEntriesAsync(ct);
        if (!result.Succeeded)
        {
            this.Error(result.Message);
        }

        return View(result.Value ?? []);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var result = await api.CancelEntryAsync(id, ct);
        if (result.Succeeded && result.Value is not null)
        {
            this.Success($"Your entry for {result.Value.EventName} has been withdrawn.");
        }
        else
        {
            this.Error(result.Message);
        }

        return RedirectToAction(nameof(Index));
    }
}
