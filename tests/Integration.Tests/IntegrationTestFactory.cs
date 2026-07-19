using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Spins up a real PostgreSQL container via Testcontainers, boots the full ASP.NET Core
/// pipeline, and runs EF Core migrations — identical to production startup.
///
/// Shared across all tests in a collection to avoid the per-test container overhead
/// (~1-2 s start time). Test isolation is achieved through unique GUIDs per test.
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override the connection string so EF Core targets the test container,
        // not whatever is configured in appsettings.json.
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                // Empty Authority → AnonymousCurrentUser (no OIDC needed in tests).
                ["Auth:Authority"] = ""
            }));
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync() => await _postgres.DisposeAsync();
}
