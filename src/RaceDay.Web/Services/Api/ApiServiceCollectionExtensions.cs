using Microsoft.Extensions.Options;

namespace RaceDay.Web.Services.Api;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Registers the typed RaceDay API client, its bearer-token handler and the session token store.</summary>
    public static IServiceCollection AddRaceDayApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "Api:BaseUrl must be an absolute URL.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ITokenStore, SessionTokenStore>();
        services.AddTransient<BearerTokenHandler>();

        static void Configure(IServiceProvider sp, HttpClient client)
        {
            var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        // Used only for refreshing tokens, so it must not go through the bearer handler itself.
        services.AddHttpClient(BearerTokenHandler.RefreshClientName, Configure);

        services.AddHttpClient<IRaceDayApi, RaceDayApiClient>(Configure)
            .AddHttpMessageHandler<BearerTokenHandler>();

        return services;
    }

    private static void TryAddSingleton<T>(this IServiceCollection services, T instance) where T : class
    {
        if (services.All(d => d.ServiceType != typeof(T)))
        {
            services.AddSingleton(instance);
        }
    }
}
