using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

/// <summary>An event summary plus where its card should link to (public, participant or organizer view).</summary>
public record EventCardModel(EventSummary Event, string Url, bool ShowStatus = false);
