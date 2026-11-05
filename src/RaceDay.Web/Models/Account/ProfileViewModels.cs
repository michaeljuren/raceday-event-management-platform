using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models.Account;

/// <summary>The fields the API lets a user change on their own profile (UpdateProfileRequest).</summary>
public class ProfileFormViewModel
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(20), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Club")]
    public int? ClubId { get; set; }

    [StringLength(100), Display(Name = "Emergency contact name")]
    public string? EmergencyContactName { get; set; }

    [Phone, StringLength(20), Display(Name = "Emergency contact phone")]
    public string? EmergencyContactPhone { get; set; }

    public IEnumerable<SelectListItem> Clubs { get; set; } = [];

    public static ProfileFormViewModel From(UserProfile p) => new()
    {
        FirstName = p.FirstName,
        LastName = p.LastName,
        PhoneNumber = p.PhoneNumber,
        ClubId = p.ClubId,
        EmergencyContactName = p.EmergencyContactName,
        EmergencyContactPhone = p.EmergencyContactPhone
    };

    public UpdateProfileRequest ToRequest() =>
        new(FirstName, LastName, PhoneNumber, ClubId, EmergencyContactName, EmergencyContactPhone);
}
