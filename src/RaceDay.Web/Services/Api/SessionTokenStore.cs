using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services.Api;

/// <summary>The API tokens for the signed-in user, kept server-side in ASP.NET Core session state.</summary>
public record ApiTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public interface ITokenStore
{
    ApiTokens? Get();

    void Save(TokenResponse tokens);

    void Clear();
}

/// <summary>
/// Stores the access and refresh tokens in <see cref="ISession"/>, so they never reach the browser.
/// The browser only holds the opaque session cookie and the authentication cookie.
/// </summary>
public class SessionTokenStore(IHttpContextAccessor accessor, TimeProvider clock) : ITokenStore
{
    private const string AccessKey = "api.access";
    private const string RefreshKey = "api.refresh";
    private const string ExpiresKey = "api.expires";

    private ISession? Session => accessor.HttpContext?.Session;

    public ApiTokens? Get()
    {
        var session = Session;
        var access = session?.GetString(AccessKey);
        var refresh = session?.GetString(RefreshKey);
        if (session is null || access is null || refresh is null
            || !long.TryParse(session.GetString(ExpiresKey), out var expires))
        {
            return null;
        }

        return new ApiTokens(access, refresh, DateTimeOffset.FromUnixTimeSeconds(expires));
    }

    public void Save(TokenResponse tokens)
    {
        var session = Session ?? throw new InvalidOperationException("Session state is not available.");
        session.SetString(AccessKey, tokens.AccessToken);
        session.SetString(RefreshKey, tokens.RefreshToken);
        session.SetString(ExpiresKey, clock.GetUtcNow().AddSeconds(tokens.ExpiresIn).ToUnixTimeSeconds().ToString());
    }

    public void Clear()
    {
        var session = Session;
        session?.Remove(AccessKey);
        session?.Remove(RefreshKey);
        session?.Remove(ExpiresKey);
    }
}
