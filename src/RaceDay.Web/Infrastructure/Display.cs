using System.Globalization;

namespace RaceDay.Web.Infrastructure;

/// <summary>
/// Formatting shared by every view. The API stores registration windows in UTC; the web app shows and
/// accepts them in the configured local time zone (South African Standard Time by default).
/// </summary>
public static class Display
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-ZA");

    public static TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;

    public static void UseTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
        {
            TimeZone = zone;
        }
    }

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);

    public static DateOnly Today => DateOnly.FromDateTime(ToLocal(DateTime.UtcNow));

    public static string Date(DateOnly date) => date.ToString("ddd d MMM yyyy", Culture);

    public static string ShortDate(DateOnly date) => date.ToString("d MMM yyyy", Culture);

    public static string Time(TimeOnly time) => time.ToString("HH:mm", Culture);

    public static string LocalDateTime(DateTime utc) => ToLocal(utc).ToString("d MMM yyyy, HH:mm", Culture);

    public static string Distance(decimal km) => $"{km.ToString("0.##", CultureInfo.InvariantCulture)} km";

    public static string Money(decimal amount) =>
        amount == 0 ? "Free" : "R" + amount.ToString("#,##0.00", CultureInfo.InvariantCulture);

    public static string FinishTime(TimeOnly? time) => time?.ToString(time.Value.Millisecond > 0 ? "H:mm:ss.fff" : "H:mm:ss", CultureInfo.InvariantCulture) ?? "—";

    public static string Position(int? position) => position is { } p ? $"#{p}" : "—";

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    /// <summary>Registration is open right now (the API enforces this too; this only drives the UI).</summary>
    public static bool RegistrationOpen(DateTime openUtc, DateTime closeUtc)
    {
        var now = DateTime.UtcNow;
        return now >= openUtc && now <= closeUtc;
    }
}
