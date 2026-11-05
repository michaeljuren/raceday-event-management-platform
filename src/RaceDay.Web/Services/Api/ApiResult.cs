using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RaceDay.Web.Services.Api;

/// <summary>
/// The outcome of an API call: either a value, or the API's ProblemDetails (title, detail and any
/// field-level validation errors) so controllers can show them to the user.
/// </summary>
public class ApiResult
{
    public HttpStatusCode StatusCode { get; init; }

    public bool Succeeded => (int)StatusCode is >= 200 and < 300;

    public string? Title { get; init; }

    public string? Detail { get; init; }

    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    /// <summary>A single human-readable sentence describing the failure.</summary>
    public string Message =>
        Detail
        ?? Errors.SelectMany(e => e.Value).FirstOrDefault()
        ?? Title
        ?? $"The request failed ({(int)StatusCode}).";

    /// <summary>
    /// Copies the API's errors into ModelState. Errors whose key matches a property of
    /// <typeparamref name="TModel"/> are attached to that field; anything else goes to the summary.
    /// </summary>
    public void AddTo<TModel>(ModelStateDictionary modelState, string prefix = "")
    {
        var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (Errors.Count == 0)
        {
            modelState.AddModelError(string.Empty, Message);
            return;
        }

        foreach (var (rawKey, messages) in Errors)
        {
            // System.Text.Json binding errors arrive as "$.eventDate"; plain validation errors as "EventDate".
            var key = rawKey.TrimStart('$', '.');
            var property = properties.FirstOrDefault(p => string.Equals(p, key, StringComparison.OrdinalIgnoreCase));
            var target = property is null ? string.Empty : prefix + property;
            foreach (var message in messages)
            {
                modelState.AddModelError(target, message);
            }
        }
    }
}

public class ApiResult<T> : ApiResult
{
    public T? Value { get; init; }
}
