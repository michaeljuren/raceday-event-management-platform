using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Tests.Infrastructure;

/// <summary>
/// Hosts the real API pipeline (routing, JWT authentication, session validation, authorization)
/// in memory, with SQL Server swapped for an isolated EF Core in-memory database that is seeded
/// with the same HasData rows as the migration.
/// </summary>
public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RaceDayDbContext>>();
            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IBannerStorage>();
            services.AddSingleton<IBannerStorage>(BannerStorage);
        });
    }

    /// <summary>The fake blob store used instead of Azure, for asserting on uploaded banners.</summary>
    public InMemoryBannerStorage BannerStorage { get; } = new();

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        EnsureDatabaseCreated();
    }

    private bool _created;
    private readonly Lock _gate = new();

    private void EnsureDatabaseCreated()
    {
        lock (_gate)
        {
            if (_created)
            {
                return;
            }

            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated();
            _created = true;
        }
    }

    /// <summary>Runs work directly against the test database (for arranging data and asserting on stored rows).</summary>
    public async Task<T> WithDbAsync<T>(Func<RaceDayDbContext, Task<T>> work)
    {
        EnsureDatabaseCreated();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        return await work(db);
    }

    public Task WithDbAsync(Func<RaceDayDbContext, Task> work) =>
        WithDbAsync(async db => { await work(db); return true; });
}
