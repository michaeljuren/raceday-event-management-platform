using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class EventRequest
{
    /// <summary>Venue (location) the event is held at.</summary>
    [Required]
    public int? VenueId { get; set; }

    [Required, StringLength(150)]
    public string EventName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Event type: Running, Walking, Cycling or Multisport (run, walk and cycle are accepted).</summary>
    [Required]
    public string Discipline { get; set; } = string.Empty;

    [Required]
    public DateOnly? EventDate { get; set; }

    /// <summary>Start time, e.g. "06:30:00".</summary>
    [Required]
    public TimeOnly? StartTime { get; set; }

    [Required]
    public DateTime? RegistrationOpenDate { get; set; }

    [Required]
    public DateTime? RegistrationCloseDate { get; set; }

    [Url, StringLength(500)]
    public string? BannerImageUrl { get; set; }
}

public class EventQuery : PageQuery
{
    /// <summary>Matches event name or description.</summary>
    public string? Search { get; set; }

    public string? Discipline { get; set; }

    public string? Province { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    /// <summary>When true, organizers only see the events they organise (all statuses).</summary>
    public bool Mine { get; set; }
}

public record EventVenueSummary(int VenueId, string VenueName, string City, string Province);

public record EventSummaryResponse(
    int EventId,
    string EventName,
    string Discipline,
    DateOnly EventDate,
    TimeOnly StartTime,
    string Status,
    int OrganizerId,
    string OrganizerName,
    EventVenueSummary Venue,
    IReadOnlyList<decimal> DistancesKm,
    string? BannerImageUrl)
{
    public static EventSummaryResponse From(Event e) => new(
        e.EventId, e.EventName, e.Discipline, e.EventDate, e.StartTime, e.Status,
        e.OrganizerId, $"{e.Organizer.FirstName} {e.Organizer.LastName}",
        new EventVenueSummary(e.Venue.VenueId, e.Venue.VenueName, e.Venue.City, e.Venue.Province),
        e.Categories.Select(c => c.DistanceKm).Distinct().OrderBy(d => d).ToList(),
        e.BannerImageUrl);
}

public record BannerUploadResponse(int EventId, string BannerImageUrl);

public record EventDetailResponse(
    int EventId,
    string EventName,
    string? Description,
    string Discipline,
    DateOnly EventDate,
    TimeOnly StartTime,
    DateTime RegistrationOpenDate,
    DateTime RegistrationCloseDate,
    string Status,
    string? BannerImageUrl,
    int OrganizerId,
    string OrganizerName,
    EventVenueSummary Venue,
    IReadOnlyList<decimal> DistancesKm,
    IReadOnlyList<CategoryResponse> Categories,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static EventDetailResponse From(Event e) => new(
        e.EventId, e.EventName, e.Description, e.Discipline, e.EventDate, e.StartTime,
        e.RegistrationOpenDate, e.RegistrationCloseDate, e.Status, e.BannerImageUrl,
        e.OrganizerId, $"{e.Organizer.FirstName} {e.Organizer.LastName}",
        new EventVenueSummary(e.Venue.VenueId, e.Venue.VenueName, e.Venue.City, e.Venue.Province),
        e.Categories.Select(c => c.DistanceKm).Distinct().OrderBy(d => d).ToList(),
        e.Categories.OrderBy(c => c.DistanceKm).ThenBy(c => c.CategoryName).Select(CategoryResponse.From).ToList(),
        e.CreatedAt, e.UpdatedAt);
}
