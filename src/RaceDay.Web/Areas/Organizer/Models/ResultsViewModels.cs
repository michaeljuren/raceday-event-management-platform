using System.Globalization;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Areas.Organizer.Models;

/// <summary>One participant's row in the results grid: their entry plus the result captured so far, if any.</summary>
public record ResultRow(RosterEntry Entry, Result? Result);

public class ResultsPageViewModel
{
    public static readonly string[] FinishStatuses = ["Finished", "DNF", "DNS", "DSQ"];

    public required EventDetail Event { get; init; }

    public required IReadOnlyList<ResultRow> Rows { get; init; }

    public bool RaceDayReached => Event.EventDate <= Display.Today;

    public bool CanCapture => RaceDayReached && Event.Status is "Published" or "Completed";

    public bool CanPublish => RaceDayReached && Event.Status == "Published";

    public int Captured => Rows.Count(r => r.Result is not null);
}

/// <summary>What a single row of the results grid posts back.</summary>
public class ResultRowForm
{
    private static readonly string[] TimeFormats = ["H:mm:ss", "HH:mm:ss", "H:mm:ss.FFF", "HH:mm:ss.FFF", "H:mm", "HH:mm"];

    public int EntryId { get; set; }

    public int? ResultId { get; set; }

    public string? FinishTime { get; set; }

    public string FinishStatus { get; set; } = "Finished";

    public int? OverallPosition { get; set; }

    public int? CategoryPosition { get; set; }

    public int? GenderPosition { get; set; }

    /// <summary>Accepts "1:52:34", "01:52:34" or "0:48:10.250"; null when blank or unreadable.</summary>
    public bool TryGetFinishTime(out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(FinishTime))
        {
            return true;
        }

        if (TimeOnly.TryParseExact(FinishTime.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = parsed;
            return true;
        }

        return false;
    }
}
