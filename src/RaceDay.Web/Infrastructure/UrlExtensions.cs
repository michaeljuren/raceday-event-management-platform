using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Infrastructure;

public static class UrlExtensions
{
    /// <summary>Participants get the detail page with the enrol form; everyone else gets the public one.</summary>
    public static string EventDetails(this IUrlHelper url, ClaimsPrincipal user, int eventId) =>
        user.IsParticipant()
            ? url.Action("Details", "Events", new { area = "Participant", id = eventId })!
            : url.Action("Details", "Events", new { area = "", id = eventId })!;
}
