using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Areas.Organizer.Models;

public class CategoryFormViewModel
{
    public static readonly string[] GenderOptions = ["Open", "Male", "Female"];

    public int? CategoryId { get; set; }

    public int EventId { get; set; }

    [Required, StringLength(100), Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Required, Range(0.01, 9999.99), Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Range(0, 120), Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Display(Name = "Who can enter")]
    public string GenderRestriction { get; set; } = "Open";

    [Required, Range(0, 999999.99), Display(Name = "Entry fee (R)")]
    public decimal? EntryFee { get; set; } = 0;

    [Range(1, int.MaxValue), Display(Name = "Spots available")]
    public int? MaxParticipants { get; set; }

    public static CategoryFormViewModel From(Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        DistanceKm = c.DistanceKm,
        MinAge = c.MinAge,
        GenderRestriction = c.GenderRestriction,
        EntryFee = c.EntryFee,
        MaxParticipants = c.MaxParticipants
    };

    public CategoryRequest ToRequest(int? routeId = null) =>
        new(routeId, CategoryName, DistanceKm ?? 0, MinAge, GenderRestriction, EntryFee ?? 0, MaxParticipants);
}

public class CategoriesPageViewModel
{
    public required EventDetail Event { get; init; }

    public CategoryFormViewModel NewCategory { get; init; } = new();
}
