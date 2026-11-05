using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

/// <summary>
/// Writes banners to an Azure Blob Storage container as <c>events/{eventId}/{guid}.{ext}</c>.
/// The container is created with anonymous blob read access so the web front end can show the images
/// directly; if the storage account forbids public access the container is created private instead.
/// </summary>
public class AzureBlobBannerStorage : IBannerStorage
{
    private readonly BlobContainerClient? _container;
    private readonly ILogger<AzureBlobBannerStorage> _logger;
    private readonly SemaphoreSlim _createGate = new(1, 1);
    private bool _containerReady;

    public AzureBlobBannerStorage(IOptions<StorageOptions> options, ILogger<AzureBlobBannerStorage> logger)
    {
        _logger = logger;
        var settings = options.Value;
        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            _container = new BlobContainerClient(settings.ConnectionString, settings.BannerContainer);
        }
    }

    public bool IsConfigured => _container is not null;

    public async Task<string> UploadAsync(int eventId, Stream content, string contentType, CancellationToken ct = default)
    {
        var container = _container ?? throw new InvalidOperationException("Banner storage is not configured.");
        if (!BannerRules.ContentTypes.TryGetValue(contentType, out var extension))
        {
            throw new ArgumentException($"Unsupported banner content type '{contentType}'.", nameof(contentType));
        }

        try
        {
            await EnsureContainerAsync(container, ct);

            var blob = container.GetBlobClient($"events/{eventId}/{Guid.NewGuid():N}{extension}");
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "public, max-age=86400" }
            }, ct);

            return blob.Uri.ToString();
        }
        catch (Exception ex) when (ex is RequestFailedException or AggregateException)
        {
            _logger.LogError(ex, "Uploading a banner for event {EventId} to blob storage failed.", eventId);
            throw new BannerStorageUnavailableException(ex);
        }
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (_container is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var prefix = _container.Uri.AbsoluteUri.TrimEnd('/') + "/";
        if (!uri.AbsoluteUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var blobName = Uri.UnescapeDataString(uri.AbsoluteUri[prefix.Length..]);
        try
        {
            await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
        }
        catch (RequestFailedException ex)
        {
            // A stale banner left behind is harmless; never fail the request because of it.
            _logger.LogWarning(ex, "Could not delete old banner blob {BlobName}", blobName);
        }
    }

    private async Task EnsureContainerAsync(BlobContainerClient container, CancellationToken ct)
    {
        if (_containerReady)
        {
            return;
        }

        await _createGate.WaitAsync(ct);
        try
        {
            if (_containerReady)
            {
                return;
            }

            try
            {
                await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "PublicAccessNotPermitted")
            {
                _logger.LogWarning("Storage account does not allow public blob access; creating {Container} as private. "
                    + "Banner URLs will not render in browsers until anonymous read access is allowed.", container.Name);
                await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            }

            _containerReady = true;
        }
        finally
        {
            _createGate.Release();
        }
    }
}
