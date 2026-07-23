using System.Text.Json;
using System.Text.Json.Serialization;
using ChatBoard.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ChatBoard.Api.Tests;

/// <summary>
/// Spins up the real API against a private in-memory database so endpoint behaviour
/// (routing, model binding, the move transaction, promote idempotency) is exercised
/// end to end. Each factory gets its own store, so tests don't bleed into each other.
/// </summary>
public class BoardApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"board-{Guid.NewGuid():N}";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Drop the app's Sqlite registration (its provider config lives across a few
            // descriptor types in EF9) so the in-memory provider is the only one left.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<BoardDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BoardDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                // The move endpoint opens a transaction; the in-memory provider has none, and that's fine here.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    /// <summary>Run an action against a fresh DbContext scope — for seeding and for asserting persisted state.</summary>
    public async Task WithDbAsync(Func<BoardDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoardDbContext>();
        await action(db);
    }
}
