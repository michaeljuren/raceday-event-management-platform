using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services.Api;

/// <summary>Every RaceDay API operation the web app uses.</summary>
public interface IRaceDayApi
{
    // Auth
    Task<ApiResult<UserSummary>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ApiResult> LogoutAsync(CancellationToken ct = default);

    // Events
    Task<ApiResult<PagedResult<EventSummary>>> GetEventsAsync(EventQuery query, CancellationToken ct = default);
    Task<ApiResult<EventDetail>> GetEventAsync(int id, CancellationToken ct = default);
    Task<ApiResult<EventDetail>> CreateEventAsync(EventRequest request, CancellationToken ct = default);
    Task<ApiResult<EventDetail>> UpdateEventAsync(int id, EventRequest request, CancellationToken ct = default);
    Task<ApiResult> DeleteEventAsync(int id, CancellationToken ct = default);
    Task<ApiResult<EventDetail>> PublishEventAsync(int id, CancellationToken ct = default);
    Task<ApiResult<EventDetail>> CompleteEventAsync(int id, CancellationToken ct = default);
    Task<ApiResult<BannerUploadResponse>> UploadBannerAsync(int id, IFormFile file, CancellationToken ct = default);
    Task<ApiResult<List<DashboardEvent>>> GetDashboardAsync(CancellationToken ct = default);

    // Categories
    Task<ApiResult<Category>> GetCategoryAsync(int id, CancellationToken ct = default);
    Task<ApiResult<Category>> CreateCategoryAsync(int eventId, CategoryRequest request, CancellationToken ct = default);
    Task<ApiResult<Category>> UpdateCategoryAsync(int id, CategoryRequest request, CancellationToken ct = default);
    Task<ApiResult> DeleteCategoryAsync(int id, CancellationToken ct = default);

    // Entries
    Task<ApiResult<Entry>> EnterAsync(int categoryId, CancellationToken ct = default);
    Task<ApiResult<List<Entry>>> GetMyEntriesAsync(CancellationToken ct = default);
    Task<ApiResult<Entry>> CancelEntryAsync(int entryId, CancellationToken ct = default);
    Task<ApiResult<List<RosterEntry>>> GetRosterAsync(int eventId, CancellationToken ct = default);

    // Results
    Task<ApiResult<List<Result>>> GetEventResultsAsync(int eventId, CancellationToken ct = default);
    Task<ApiResult<Result>> CreateResultAsync(int entryId, ResultRequest request, CancellationToken ct = default);
    Task<ApiResult<Result>> UpdateResultAsync(int resultId, ResultRequest request, CancellationToken ct = default);

    // Users and reference data
    Task<ApiResult<UserProfile>> GetProfileAsync(CancellationToken ct = default);
    Task<ApiResult<UserProfile>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<ApiResult<List<PerformanceHistoryItem>>> GetPerformanceHistoryAsync(CancellationToken ct = default);
    Task<ApiResult<List<Venue>>> GetVenuesAsync(CancellationToken ct = default);
    Task<ApiResult<List<Club>>> GetClubsAsync(CancellationToken ct = default);
}

/// <summary>Typed <see cref="HttpClient"/> over the RaceDay API. Contains no business rules: the API owns them.</summary>
public class RaceDayApiClient(HttpClient http) : IRaceDayApi
{
    private const int MaxPageSize = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<UserSummary>> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        SendAsync<UserSummary>(HttpMethod.Post, "api/auth/register", request, ct);

    public Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        SendAsync<TokenResponse>(HttpMethod.Post, "api/auth/login", request, ct);

    public Task<ApiResult> LogoutAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "api/auth/logout", null, ct);

    public Task<ApiResult<PagedResult<EventSummary>>> GetEventsAsync(EventQuery query, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["search"] = query.Search,
            ["discipline"] = query.Discipline,
            ["province"] = query.Province,
            ["fromDate"] = query.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["toDate"] = query.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["mine"] = query.Mine ? "true" : null,
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture)
        }.Where(p => !string.IsNullOrWhiteSpace(p.Value));

        return SendAsync<PagedResult<EventSummary>>(HttpMethod.Get, QueryHelpers.AddQueryString("api/events", parameters), null, ct);
    }

    public Task<ApiResult<EventDetail>> GetEventAsync(int id, CancellationToken ct = default) =>
        SendAsync<EventDetail>(HttpMethod.Get, $"api/events/{id}", null, ct);

    public Task<ApiResult<EventDetail>> CreateEventAsync(EventRequest request, CancellationToken ct = default) =>
        SendAsync<EventDetail>(HttpMethod.Post, "api/events", request, ct);

    public Task<ApiResult<EventDetail>> UpdateEventAsync(int id, EventRequest request, CancellationToken ct = default) =>
        SendAsync<EventDetail>(HttpMethod.Put, $"api/events/{id}", request, ct);

    public Task<ApiResult> DeleteEventAsync(int id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"api/events/{id}", null, ct);

    public Task<ApiResult<EventDetail>> PublishEventAsync(int id, CancellationToken ct = default) =>
        SendAsync<EventDetail>(HttpMethod.Post, $"api/events/{id}/publish", null, ct);

    public Task<ApiResult<EventDetail>> CompleteEventAsync(int id, CancellationToken ct = default) =>
        SendAsync<EventDetail>(HttpMethod.Post, $"api/events/{id}/complete", null, ct);

    public async Task<ApiResult<BannerUploadResponse>> UploadBannerAsync(int id, IFormFile file, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        using var form = new MultipartFormDataContent { { content, "file", Path.GetFileName(file.FileName) } };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/events/{id}/banner") { Content = form };
        return await ExecuteAsync<BannerUploadResponse>(request, ct);
    }

    public Task<ApiResult<List<DashboardEvent>>> GetDashboardAsync(CancellationToken ct = default) =>
        SendAsync<List<DashboardEvent>>(HttpMethod.Get, "api/events/dashboard", null, ct);

    public Task<ApiResult<Category>> GetCategoryAsync(int id, CancellationToken ct = default) =>
        SendAsync<Category>(HttpMethod.Get, $"api/categories/{id}", null, ct);

    public Task<ApiResult<Category>> CreateCategoryAsync(int eventId, CategoryRequest request, CancellationToken ct = default) =>
        SendAsync<Category>(HttpMethod.Post, $"api/events/{eventId}/categories", request, ct);

    public Task<ApiResult<Category>> UpdateCategoryAsync(int id, CategoryRequest request, CancellationToken ct = default) =>
        SendAsync<Category>(HttpMethod.Put, $"api/categories/{id}", request, ct);

    public Task<ApiResult> DeleteCategoryAsync(int id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"api/categories/{id}", null, ct);

    public Task<ApiResult<Entry>> EnterAsync(int categoryId, CancellationToken ct = default) =>
        SendAsync<Entry>(HttpMethod.Post, $"api/categories/{categoryId}/entries", null, ct);

    public Task<ApiResult<List<Entry>>> GetMyEntriesAsync(CancellationToken ct = default) =>
        SendAsync<List<Entry>>(HttpMethod.Get, "api/users/me/entries", null, ct);

    public Task<ApiResult<Entry>> CancelEntryAsync(int entryId, CancellationToken ct = default) =>
        SendAsync<Entry>(HttpMethod.Put, $"api/entries/{entryId}/cancel", null, ct);

    public Task<ApiResult<List<RosterEntry>>> GetRosterAsync(int eventId, CancellationToken ct = default) =>
        GetAllPagesAsync<RosterEntry>($"api/events/{eventId}/entries", ct);

    public Task<ApiResult<List<Result>>> GetEventResultsAsync(int eventId, CancellationToken ct = default) =>
        GetAllPagesAsync<Result>($"api/events/{eventId}/results", ct);

    public Task<ApiResult<Result>> CreateResultAsync(int entryId, ResultRequest request, CancellationToken ct = default) =>
        SendAsync<Result>(HttpMethod.Post, $"api/entries/{entryId}/result", request, ct);

    public Task<ApiResult<Result>> UpdateResultAsync(int resultId, ResultRequest request, CancellationToken ct = default) =>
        SendAsync<Result>(HttpMethod.Put, $"api/results/{resultId}", request, ct);

    public Task<ApiResult<UserProfile>> GetProfileAsync(CancellationToken ct = default) =>
        SendAsync<UserProfile>(HttpMethod.Get, "api/users/me", null, ct);

    public Task<ApiResult<UserProfile>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default) =>
        SendAsync<UserProfile>(HttpMethod.Put, "api/users/me", request, ct);

    public Task<ApiResult<List<PerformanceHistoryItem>>> GetPerformanceHistoryAsync(CancellationToken ct = default) =>
        SendAsync<List<PerformanceHistoryItem>>(HttpMethod.Get, "api/users/me/performance-history", null, ct);

    public Task<ApiResult<List<Venue>>> GetVenuesAsync(CancellationToken ct = default) =>
        GetAllPagesAsync<Venue>("api/venues", ct);

    public Task<ApiResult<List<Club>>> GetClubsAsync(CancellationToken ct = default) =>
        GetAllPagesAsync<Club>("api/clubs", ct);

    /// <summary>Follows the API's paging until every item has been read.</summary>
    private async Task<ApiResult<List<T>>> GetAllPagesAsync<T>(string path, CancellationToken ct)
    {
        var items = new List<T>();
        for (var page = 1; ; page++)
        {
            var separator = path.Contains('?') ? '&' : '?';
            var result = await SendAsync<PagedResult<T>>(HttpMethod.Get, $"{path}{separator}page={page}&pageSize={MaxPageSize}", null, ct);
            if (!result.Succeeded || result.Value is null)
            {
                return Failure<List<T>>(result);
            }

            items.AddRange(result.Value.Items);
            if (page >= result.Value.TotalPages)
            {
                return new ApiResult<List<T>> { StatusCode = HttpStatusCode.OK, Value = items };
            }
        }
    }

    private async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct) =>
        await SendAsync<object>(method, path, body, ct);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        return await ExecuteAsync<T>(request, ct);
    }

    private async Task<ApiResult<T>> ExecuteAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var hasBody = response.StatusCode != HttpStatusCode.NoContent && response.Content.Headers.ContentLength != 0;
            var value = hasBody && typeof(T) != typeof(object)
                ? await response.Content.ReadFromJsonAsync<T>(Json, ct)
                : default;
            return new ApiResult<T> { StatusCode = response.StatusCode, Value = value };
        }

        return await ReadProblemAsync<T>(response, ct);
    }

    private static async Task<ApiResult<T>> ReadProblemAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        ProblemBody? problem = null;
        try
        {
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json") == true)
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Json, ct);
            }
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body; fall back to the status code.
        }

        var fallback = response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "You do not have permission to do that.",
            HttpStatusCode.NotFound => "That item could not be found.",
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            _ => null
        };

        return new ApiResult<T>
        {
            StatusCode = response.StatusCode,
            Title = problem?.Title,
            Detail = problem?.Detail ?? (problem?.Errors is { Count: > 0 } ? null : fallback),
            Errors = problem?.Errors ?? new Dictionary<string, string[]>()
        };
    }

    private static ApiResult<TOut> Failure<TOut>(ApiResult source) => new()
    {
        StatusCode = source.StatusCode,
        Title = source.Title,
        Detail = source.Detail,
        Errors = source.Errors
    };

    private sealed record ProblemBody(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
