using System.Security.Claims;

namespace RaceDay.Web.Infrastructure;

/// <summary>Role names as returned by the API.</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Organizer = "Organizer";
    public const string Participant = "Participant";

    public const string OrganizerOrAdmin = Organizer + "," + Admin;

    public static bool IsOrganizerOrAdmin(this ClaimsPrincipal user) => user.IsInRole(Organizer) || user.IsInRole(Admin);

    public static bool IsParticipant(this ClaimsPrincipal user) => user.IsInRole(Participant);

    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Where a user lands after logging in when there is no return URL.</summary>
    public static string HomeUrl(this ClaimsPrincipal user) =>
        user.IsOrganizerOrAdmin() ? "/Organizer/Dashboard"
        : user.IsParticipant() ? "/Participant/Home"
        : "/";
}
