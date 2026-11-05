using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Areas.Organizer.Models;

/// <summary>
/// Create / edit form for an event. Annotations only give quick feedback in the browser; the API applies
/// the real rules (venue exists, registration closes before race day, no events in the past, ...).
/// </summary>
public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required, Display(Name = "Venue")]
    public int? VenueId { get; set; }

    [Required, StringLength(150), Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Required, Display(Name = "Event type")]
    public string Discipline { get; set; } = "Running";

    [Required, Display(Name = "Race date")]
    public DateOnly? EventDate { get; set; }

    [Required, Display(Name = "Start time")]
    public TimeOnly? StartTime { get; set; }

    [Required, Display(Name = "Entries open")]
    public DateTime? RegistrationOpenDate { get; set; }

    [Required, Display(Name = "Entries close")]
    public DateTime? RegistrationCloseDate { get; set; }

    /// <summary>The banner already stored for the event, kept so an edit does not clear it.</summary>
    public string? BannerImageUrl { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    // A first distance can be added straight from the create form; more are managed on the Categories page.
    [Range(0.01, 9999.99), Display(Name = "Distance (km)")]
    public decimal? InitialDistanceKm { get; set; }

    [Range(0, 999999.99), Display(Name = "Entry fee (R)")]
    public decimal? InitialEntryFee { get; set; }

    public string Status { get; set; } = "Draft";

    public IReadOnlyList<decimal> DistancesKm { get; set; } = [];

    public IEnumerable<SelectListItem> Venues { get; set; } = [];

    public bool IsNew => EventId is null;

    public static EventFormViewModel ForNew()
    {
        var now = Display.ToLocal(DateTime.UtcNow);
        var raceDay = DateOnly.FromDateTime(now).AddDays(60);
        return new EventFormViewModel
        {
            EventDate = raceDay,
            StartTime = new TimeOnly(6, 30),
            RegistrationOpenDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0),
            RegistrationCloseDate = raceDay.AddDays(-3).ToDateTime(new TimeOnly(23, 59))
        };
    }

    public static EventFormViewModel From(EventDetail e) => new()
    {
        EventId = e.EventId,
        VenueId = e.Venue.VenueId,
        EventName = e.EventName,
        Description = e.Description,
        Discipline = e.Discipline,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        RegistrationOpenDate = Display.ToLocal(e.RegistrationOpenDate),
        RegistrationCloseDate = Display.ToLocal(e.RegistrationCloseDate),
        BannerImageUrl = e.BannerImageUrl,
        Status = e.Status,
        DistancesKm = e.DistancesKm
    };

    public EventRequest ToRequest() => new(
        VenueId, EventName, Description, Discipline, EventDate, StartTime,
        RegistrationOpenDate is { } open ? Display.ToUtc(open) : null,
        RegistrationCloseDate is { } close ? Display.ToUtc(close) : null,
        string.IsNullOrWhiteSpace(BannerImageUrl) ? null : BannerImageUrl);
}
