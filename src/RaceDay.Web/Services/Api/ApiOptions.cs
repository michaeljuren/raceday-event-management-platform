namespace RaceDay.Web.Services.Api;

/// <summary>Where the RaceDay API lives (section "Api"; override with <c>Api__BaseUrl</c>).</summary>
public class ApiOptions
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = "https://localhost:7245";

    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Thrown when the user's API session can no longer be used (expired or revoked refresh token).</summary>
public class ApiSessionExpiredException() : Exception("Your session has expired. Please log in again.");
