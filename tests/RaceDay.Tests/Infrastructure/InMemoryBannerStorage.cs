using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Tests.Infrastructure;

/// <summary>Stands in for Azure Blob Storage so banner uploads can be tested without a storage account.</summary>
public class InMemoryBannerStorage : IBannerStorage
{
    public const string BaseUrl = "https://teststorage.blob.core.windows.net/event-banners/";

    public ConcurrentDictionary<string, (byte[] Content, string ContentType)> Blobs { get; } = new();

    public bool IsConfigured => true;

    public async Task<string> UploadAsync(int eventId, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var url = $"{BaseUrl}events/{eventId}/{Guid.NewGuid():N}{BannerRules.ContentTypes[contentType]}";
        Blobs[url] = (buffer.ToArray(), contentType);
        return url;
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        Blobs.TryRemove(url, out _);
        return Task.CompletedTask;
    }
}
