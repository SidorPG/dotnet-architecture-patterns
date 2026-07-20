using DotNet.Testcontainers.Builders;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
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
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Auth:Authority"] = "",
                // No RabbitMQ:Host → DependencyInjection falls back to in-memory transport.
                ["RabbitMQ:Host"] = ""
            }));

        // Replace the MassTransit registration with the test harness.
        // The harness uses an in-memory transport and provides helpers for
        // asserting that messages were published / consumed in tests.
        builder.ConfigureServices(services =>
            services.AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<Infrastructure.Messaging.GroupJoinStateMachine,
                                      Infrastructure.Messaging.GroupJoinSagaState>();
                x.AddConsumer<Infrastructure.Messaging.Consumers.InitiatePaymentConsumer>();
                x.AddConsumer<Infrastructure.Messaging.Consumers.EnrollmentConfirmedConsumer>();
            }));
    }

    /// <summary>
    /// Creates an HTTP client pre-authorized with the specified demo permission claims.
    /// DemoAuthenticationHandler parses the token value as comma-separated claim names.
    /// </summary>
    public HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        var client = CreateClient();
        var token  = string.Join(",", permissions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync() => await _postgres.DisposeAsync();
}
