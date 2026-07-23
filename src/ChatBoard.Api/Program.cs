using System.Text.Json.Serialization;
using ChatBoard.Api.Data;
using ChatBoard.Api.Features.Cards;
using ChatBoard.Api.Features.Import;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Board") ?? "Data Source=board.db";
builder.Services.AddDbContext<BoardDbContext>(options => options.UseSqlite(connectionString));

// Statuses travel the wire as their names ("NeedsAction"), matching how they're stored.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Injected so entity timestamps are testable rather than reaching for DateTimeOffset.UtcNow inline.
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BoardDbContext>();
    // EnsureCreated() is fine while the schema settles. Once it stabilises, switch to
    // `dotnet ef migrations` and call db.Database.Migrate() here instead.
    db.Database.EnsureCreated();
}

app.MapCardEndpoints();
app.MapImportEndpoints();

app.Run();

// Exposed so the test host (WebApplicationFactory) can reference the entry point.
public partial class Program { }
