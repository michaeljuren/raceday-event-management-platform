namespace RaceDay.Api.Services;

/// <summary>Azure Blob Storage settings for event banner images (section "Storage").</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Azure Storage connection string. Use "UseDevelopmentStorage=true" for Azurite locally; supply the real
    /// value through user secrets or the <c>Storage__ConnectionString</c> environment variable. Never commit it.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Blob container that banners are written to. Created on first upload if it does not exist.</summary>
    public string BannerContainer { get; set; } = "event-banners";
}
