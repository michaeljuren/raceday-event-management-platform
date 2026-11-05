namespace RaceDay.Api.Services;

/// <summary>Stores event banner images and returns the public URL they can be displayed from.</summary>
public interface IBannerStorage
{
    /// <summary>False when no storage account has been configured, so uploads cannot be accepted.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads a banner for an event and returns its absolute URL.</summary>
    Task<string> UploadAsync(int eventId, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Deletes a previously uploaded banner. URLs that were not issued by this store are ignored.</summary>
    Task DeleteAsync(string url, CancellationToken ct = default);
}

/// <summary>The storage service could not be reached or rejected the upload.</summary>
public class BannerStorageUnavailableException(Exception inner)
    : Exception("Banner storage is currently unavailable.", inner);

/// <summary>What the API accepts as a banner image.</summary>
public static class BannerRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Allowed content types and the file extension each is stored with.</summary>
    public static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(string? contentType, long length)
    {
        if (length <= 0)
        {
            return "The banner file is empty.";
        }

        if (length > MaxBytes)
        {
            return "The banner image must be 5 MB or smaller.";
        }

        if (contentType is null || !ContentTypes.ContainsKey(contentType))
        {
            return "The banner must be a JPEG, PNG or WebP image.";
        }

        return null;
    }
}
