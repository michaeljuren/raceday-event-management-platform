using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services.Api;

/// <summary>
/// Attaches the signed-in user's access token to every API call. When the token is about to expire, or the
/// API answers 401, it exchanges the refresh token for a new pair (the API rotates refresh tokens) and
/// retries once. If refreshing fails the stored tokens are cleared and <see cref="ApiSessionExpiredException"/>
/// is thrown so the web app can sign the user out.
/// </summary>
public class BearerTokenHandler(
    ITokenStore tokens,
    IHttpClientFactory clients,
    IHttpContextAccessor accessor,
    TimeProvider clock,
    ILogger<BearerTokenHandler> logger) : DelegatingHandler
{
    public const string RefreshClientName = "RaceDayApi.Auth";

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    // One refresh at a time per browser session, so parallel requests don't burn the same refresh token.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var current = tokens.Get();
        if (current is null)
        {
            return await base.SendAsync(request, ct);
        }

        if (current.ExpiresAt - clock.GetUtcNow() < RefreshMargin)
        {
            current = await RefreshAsync(current, ct);
        }

        // Buffer the body so the request can be replayed after a refresh (multipart uploads included).
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(ct);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        current = await RefreshAsync(current, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        return await base.SendAsync(request, ct);
    }

    private async Task<ApiTokens> RefreshAsync(ApiTokens stale, CancellationToken ct)
    {
        var key = accessor.HttpContext?.Session.Id ?? string.Empty;
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // Another request may already have refreshed while this one waited.
            var latest = tokens.Get();
            if (latest is not null && latest.AccessToken != stale.AccessToken)
            {
                return latest;
            }

            var client = clients.CreateClient(RefreshClientName);
            using var response = await client.PostAsJsonAsync("api/auth/refresh-token", new RefreshTokenRequest(stale.RefreshToken), ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Refreshing the API session failed with {Status}; signing the user out.", (int)response.StatusCode);
                tokens.Clear();
                throw new ApiSessionExpiredException();
            }

            var fresh = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                ?? throw new ApiSessionExpiredException();
            tokens.Save(fresh);
            return tokens.Get()!;
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
            {
                Gates.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, gate));
            }
        }
    }
}
