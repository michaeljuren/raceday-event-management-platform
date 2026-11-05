using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

/// <summary>Query-string filters for the event listings.</summary>
public class EventFilterViewModel
{
    public static readonly string[] Disciplines = ["Running", "Walking", "Cycling", "Multisport"];

    public static readonly string[] Provinces =
    [
        "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal",
        "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape"
    ];

    [Display(Name = "Search")]
    public string? Search { get; set; }

    public string? Discipline { get; set; }

    public string? Province { get; set; }

    [Display(Name = "From"), DataType(DataType.Date)]
    public DateOnly? FromDate { get; set; }

    [Display(Name = "To"), DataType(DataType.Date)]
    public DateOnly? ToDate { get; set; }

    public int Page { get; set; } = 1;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) || !string.IsNullOrWhiteSpace(Discipline) || !string.IsNullOrWhiteSpace(Province)
        || FromDate is not null || ToDate is not null;

    /// <summary>Without an explicit date range the listings show upcoming events only.</summary>
    public EventQuery ToQuery(int pageSize = 12) => new(
        Search: Search, Discipline: Discipline, Province: Province,
        FromDate: FromDate ?? (ToDate is null ? Display.Today : null), ToDate: ToDate,
        Page: Math.Max(1, Page), PageSize: pageSize);

    public object RouteValues(int page) => new { Search, Discipline, Province, FromDate, ToDate, page };
}

public class EventListViewModel
{
    public EventFilterViewModel Filter { get; init; } = new();

    public PagedResult<EventSummary> Events { get; init; } = new([], 1, 12, 0, 0);

    public string? Error { get; init; }
}

/// <summary>Builds pager links for any listing.</summary>
public record PagerModel(int Page, int TotalPages, Func<int, string> UrlFor);
