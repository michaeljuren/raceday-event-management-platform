using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Areas.Organizer.Models;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Areas.Organizer.Controllers;

/// <summary>Capture finish times and positions after race day, then publish the results.</summary>
[Area("Organizer")]
[Authorize(Roles = AppRoles.OrganizerOrAdmin)]
public class ResultsController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Index(int eventId, CancellationToken ct)
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

        var results = (await api.GetEventResultsAsync(eventId, ct)).Value ?? [];
        var byEntry = results.ToDictionary(r => r.EntryId);

        // Everyone still holding a place, plus anyone who already has a result recorded.
        var rows = (roster.Value ?? [])
            .Where(e => e.Status is "Pending" or "Confirmed" || byEntry.ContainsKey(e.EntryId))
            .Select(e => new ResultRow(e, byEntry.GetValueOrDefault(e.EntryId)))
            .OrderBy(r => r.Result?.OverallPosition ?? int.MaxValue)
            .ThenBy(r => r.Entry.CategoryName)
            .ThenBy(r => r.Entry.LastName)
            .ToList();

        return View(new ResultsPageViewModel { Event = evt, Rows = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Save(int eventId, ResultRowForm row, CancellationToken ct)
    {
        if (!row.TryGetFinishTime(out var finishTime))
        {
            this.Error($"\"{row.FinishTime}\" is not a valid finish time. Use h:mm:ss, for example 1:52:34.");
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var finished = row.FinishStatus == "Finished";
        var request = new ResultRequest(
            finished ? finishTime : null, null, null,
            finished ? row.OverallPosition : null,
            finished ? row.CategoryPosition : null,
            finished ? row.GenderPosition : null,
            row.FinishStatus);

        var saved = row.ResultId is { } resultId
            ? await api.UpdateResultAsync(resultId, request, ct)
            : await api.CreateResultAsync(row.EntryId, request, ct);

        if (saved.Succeeded && saved.Value is not null)
        {
            this.Success($"Result saved for {saved.Value.ParticipantName}.");
        }
        else
        {
            this.Error($"The result could not be saved: {saved.Message}");
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost]
    public async Task<IActionResult> Publish(int eventId, CancellationToken ct)
    {
        var result = await api.CompleteEventAsync(eventId, ct);
        if (result.Succeeded)
        {
            this.Success("Results published. Participants can now see their times and positions.");
        }
        else
        {
            this.Error(result.Message);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }
}
