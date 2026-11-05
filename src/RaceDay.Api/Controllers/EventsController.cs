using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Race days. Organizers manage their own events; everyone can browse published ones.</summary>
[Route("api/events")]
public class EventsController(RaceDayDbContext db, TimeProvider clock) : ApiControllerBase
{
    /// <summary>Browse and search events.</summary>
    /// <remarks>
    /// Anonymous users and participants see every event that is not a Draft. Organizers additionally see
    /// their own drafts (or only their own events with <c>mine=true</c>); admins see everything.
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<EventSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] EventQuery query, CancellationToken ct)
    {
        var events = db.Events.AsNoTracking()
            .Include(e => e.Organizer)
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .AsQueryable();

        int? me = IsAuthenticated ? CurrentUserId : null;
        if (query.Mine && me is not null)
        {
            events = events.Where(e => e.OrganizerId == me);
        }
        else if (!IsAdmin)
        {
            events = events.Where(e => e.Status != EventStatus.Draft || e.OrganizerId == me);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            events = events.Where(e => e.EventName.Contains(term) || (e.Description != null && e.Description.Contains(term)));
        }

        if (Discipline.Normalise(query.Discipline) is { } discipline)
        {
            events = events.Where(e => e.Discipline == discipline);
        }

        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            events = events.Where(e => e.Venue.Province == query.Province);
        }

        if (query.FromDate is { } from)
        {
            events = events.Where(e => e.EventDate >= from);
        }

        if (query.ToDate is { } to)
        {
            events = events.Where(e => e.EventDate <= to);
        }

        var page = await events
            .OrderBy(e => e.EventDate).ThenBy(e => e.EventName)
            .ToPagedResultAsync(query, EventSummaryResponse.From, ct);
        return Ok(page);
    }

    /// <summary>Organizer dashboard: every event you organise with its enrolment totals.</summary>
    /// <remarks>
    /// <c>totalEnrolments</c> counts active (Pending or Confirmed) entries across all of the event's categories.
    /// Upcoming events (today or later) come first, soonest first, followed by past events, most recent first.
    /// Admins see every event.
    /// </remarks>
    [HttpGet("dashboard")]
    [Authorize(Roles = RoleNames.OrganizerOrAdmin)]
    [ProducesResponseType<List<DashboardEventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var events = db.Events.AsNoTracking();
        if (!IsAdmin)
        {
            var me = CurrentUserId;
            events = events.Where(e => e.OrganizerId == me);
        }

        var rows = await events
            .Select(e => new DashboardEventResponse(
                e.EventId,
                e.EventName,
                e.Discipline,
                e.EventDate,
                e.StartTime,
                e.Status,
                e.Venue.VenueName,
                e.Venue.City,
                e.Categories.Count,
                e.Categories.SelectMany(c => c.Entries).Count(en => EntryStatus.Active.Contains(en.Status)),
                e.Categories.SelectMany(c => c.Entries).Count(en => en.Result != null),
                e.EventDate >= today))
            .ToListAsync(ct);

        return Ok(rows
            .OrderByDescending(r => r.IsUpcoming)
            .ThenBy(r => r.IsUpcoming ? r.EventDate.DayNumber : -r.EventDate.DayNumber)
            .ThenBy(r => r.EventName)
            .ToList());
    }

    /// <summary>Get an event's detail, including its categories.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var evt = await LoadEventAsync(id, tracking: false, ct);
        return evt is null || !CanView(evt) ? NotFoundProblem($"Event {id} not found.") : Ok(EventDetailResponse.From(evt));
    }

    /// <summary>Create a new event. It starts as a Draft until it is published.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Organizer)]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, isNew: true, ct);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var evt = new Event
        {
            OrganizerId = CurrentUserId,
            Status = EventStatus.Draft,
            CreatedAt = now
        };
        Apply(evt, request, now);
        db.Events.Add(evt);
        await db.SaveChangesAsync(ct);

        var created = await LoadEventAsync(evt.EventId, tracking: false, ct);
        return CreatedAtAction(nameof(Get), new { id = evt.EventId }, EventDetailResponse.From(created!));
    }

    /// <summary>Update an event.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.OrganizerOrAdmin)]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest request, CancellationToken ct)
    {
        var evt = await LoadEventAsync(id, tracking: true, ct);
        if (evt is null)
        {
            return NotFoundProblem($"Event {id} not found.");
        }

        if (!CanManage(evt))
        {
            return ForbiddenProblem("Only the event's organizer or an admin can update it.");
        }

        await ValidateAsync(request, isNew: false, ct);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        Apply(evt, request, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);

        var updated = await LoadEventAsync(id, tracking: false, ct);
        return Ok(EventDetailResponse.From(updated!));
    }

    /// <summary>Delete or cancel an event.</summary>
    /// <remarks>
    /// An event with no entries is deleted (with its categories). An event that already has entries is
    /// cancelled instead: its status becomes Cancelled and its entries are cancelled. An event with
    /// recorded results cannot be removed (409).
    /// </remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.OrganizerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var evt = await db.Events
            .Include(e => e.Categories).ThenInclude(c => c.Entries).ThenInclude(en => en.Result)
            .SingleOrDefaultAsync(e => e.EventId == id, ct);
        if (evt is null)
        {
            return NotFoundProblem($"Event {id} not found.");
        }

        if (!CanManage(evt))
        {
            return ForbiddenProblem("Only the event's organizer or an admin can delete it.");
        }

        var entries = evt.Categories.SelectMany(c => c.Entries).ToList();
        if (entries.Any(en => en.Result is not null))
        {
            return ConflictProblem("Results have been recorded for this event, so it cannot be deleted or cancelled.");
        }

        if (entries.Count == 0)
        {
            db.Categories.RemoveRange(evt.Categories);
            db.Events.Remove(evt);
        }
        else
        {
            evt.Status = EventStatus.Cancelled;
            evt.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            foreach (var entry in entries.Where(en => EntryStatus.Active.Contains(en.Status)))
            {
                entry.Status = EntryStatus.Cancelled;
            }
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Publish a Draft event so participants can see and enter it.</summary>
    [HttpPost("{id:int}/publish")]
    [Authorize(Roles = RoleNames.OrganizerOrAdmin)]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        var evt = await LoadEventAsync(id, tracking: true, ct);
        if (evt is null)
        {
            return NotFoundProblem($"Event {id} not found.");
        }

        if (!CanManage(evt))
        {
            return ForbiddenProblem("Only the event's organizer or an admin can publish it.");
        }

        if (evt.Status != EventStatus.Draft)
        {
            return BadRequestProblem(nameof(Event.Status), $"Only Draft events can be published; this event is {evt.Status}.");
        }

        if (evt.Categories.Count == 0)
        {
            return BadRequestProblem(nameof(Event.Categories), "Add at least one category before publishing.");
        }

        evt.Status = EventStatus.Published;
        evt.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Ok(EventDetailResponse.From(evt));
    }

    /// <summary>Mark a Published event as Completed, publishing its results.</summary>
    /// <remarks>
    /// Only allowed once the event has taken place (its date is today or earlier). Results can still be
    /// corrected afterwards with PUT /api/results/{id}.
    /// </remarks>
    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = RoleNames.OrganizerOrAdmin)]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        var evt = await LoadEventAsync(id, tracking: true, ct);
        if (evt is null)
        {
            return NotFoundProblem($"Event {id} not found.");
        }

        if (!CanManage(evt))
        {
            return ForbiddenProblem("Only the event's organizer or an admin can publish its results.");
        }

        if (evt.Status != EventStatus.Published)
        {
            return BadRequestProblem(nameof(Event.Status), $"Only Published events can be completed; this event is {evt.Status}.");
        }

        if (evt.EventDate > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
        {
            return BadRequestProblem(nameof(Event.EventDate), "Results can only be published once the event has taken place.");
        }

        evt.Status = EventStatus.Completed;
        evt.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Ok(EventDetailResponse.From(evt));
    }

    /// <summary>Upload (or replace) the event's banner image.</summary>
    /// <remarks>
    /// Send <c>multipart/form-data</c> with a single <c>file</c> field: a JPEG, PNG or WebP image of at most 5 MB.
    /// The image is stored in Azure Blob Storage and the event's <c>bannerImageUrl</c> is set to its URL.
    /// </remarks>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = RoleNames.OrganizerOrAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BannerRules.MaxBytes + 64 * 1024)]
    [ProducesResponseType<BannerUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int id, IFormFile? file, [FromServices] IBannerStorage storage, CancellationToken ct)
    {
        var evt = await db.Events.SingleOrDefaultAsync(e => e.EventId == id, ct);
        if (evt is null)
        {
            return NotFoundProblem($"Event {id} not found.");
        }

        if (!CanManage(evt))
        {
            return ForbiddenProblem("Only the event's organizer or an admin can change its banner.");
        }

        if (file is null)
        {
            return BadRequestProblem("file", "Choose an image file to upload.");
        }

        if (BannerRules.Validate(file.ContentType, file.Length) is { } error)
        {
            return BadRequestProblem("file", error);
        }

        if (!storage.IsConfigured)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Storage unavailable",
                detail: "Banner storage is not configured on the server.");
        }

        var previous = evt.BannerImageUrl;
        try
        {
            await using var stream = file.OpenReadStream();
            evt.BannerImageUrl = await storage.UploadAsync(id, stream, file.ContentType, ct);
        }
        catch (BannerStorageUnavailableException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Storage unavailable",
                detail: "The banner could not be stored right now. Please try again later.");
        }

        evt.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previous))
        {
            await storage.DeleteAsync(previous, ct);
        }

        return Ok(new BannerUploadResponse(id, evt.BannerImageUrl));
    }

    private Task<Event?> LoadEventAsync(int id, bool tracking, CancellationToken ct)
    {
        var query = db.Events
            .Include(e => e.Organizer)
            .Include(e => e.Venue)
            .Include(e => e.Categories).ThenInclude(c => c.Entries)
            .AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(e => e.EventId == id, ct);
    }

    private async Task ValidateAsync(EventRequest request, bool isNew, CancellationToken ct)
    {
        if (request.VenueId is { } venueId && !await db.Venues.AnyAsync(v => v.VenueId == venueId, ct))
        {
            ModelState.AddModelError(nameof(request.VenueId), $"Venue {venueId} does not exist.");
        }

        if (Discipline.Normalise(request.Discipline) is null)
        {
            ModelState.AddModelError(nameof(request.Discipline), "Discipline must be Running, Walking, Cycling or Multisport (or run, walk, cycle).");
        }

        if (request.RegistrationOpenDate >= request.RegistrationCloseDate)
        {
            ModelState.AddModelError(nameof(request.RegistrationCloseDate), "Registration must close after it opens.");
        }

        if (request.EventDate is { } eventDate)
        {
            if (request.RegistrationCloseDate is { } close && DateOnly.FromDateTime(close) > eventDate)
            {
                ModelState.AddModelError(nameof(request.RegistrationCloseDate), "Registration must close on or before the event date.");
            }

            if (isNew && eventDate < DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            {
                ModelState.AddModelError(nameof(request.EventDate), "A new event cannot be in the past.");
            }
        }
    }

    private static void Apply(Event evt, EventRequest request, DateTime now)
    {
        evt.VenueId = request.VenueId!.Value;
        evt.EventName = request.EventName.Trim();
        evt.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        evt.Discipline = Discipline.Normalise(request.Discipline)!;
        evt.EventDate = request.EventDate!.Value;
        evt.StartTime = request.StartTime!.Value;
        evt.RegistrationOpenDate = request.RegistrationOpenDate!.Value;
        evt.RegistrationCloseDate = request.RegistrationCloseDate!.Value;
        evt.BannerImageUrl = string.IsNullOrWhiteSpace(request.BannerImageUrl) ? null : request.BannerImageUrl.Trim();
        evt.UpdatedAt = now;
    }
}
