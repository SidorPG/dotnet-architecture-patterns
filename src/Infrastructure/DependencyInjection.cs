using Application.Common.Interfaces;
using Application.GroupJoinRequests;
using Infrastructure.Identity;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Persistence;
using Infrastructure.Persistence.EF;
using Infrastructure.QueryServices;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Interceptors ──────────────────────────────────────────────
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<DateTimeInterceptor>();
        // DomainEventDispatcherInterceptor now depends on IPublishEndpoint (MassTransit)
        // and is registered as scoped alongside the other interceptors.
        services.AddScoped<DomainEventDispatcherInterceptor>();

        // ── Database ──────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>()
                .GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    sp.GetRequiredService<SoftDeleteInterceptor>(),
                    sp.GetRequiredService<AuditInterceptor>(),
                    sp.GetRequiredService<DateTimeInterceptor>(),
                    sp.GetRequiredService<DomainEventDispatcherInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ── Identity ──────────────────────────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IPermissionService, PermissionService>();

        // ── Query services ────────────────────────────────────────────
        services.AddScoped<IGroupJoinRequestQueryService, GroupJoinRequestQueryService>();

        // ── MassTransit ───────────────────────────────────────────────
        var rabbitHost = configuration["RabbitMQ:Host"];

        services.AddMassTransit(x =>
        {
            // Saga — replaces GroupJoinProcessManager
            x.AddSagaStateMachine<GroupJoinStateMachine, GroupJoinSagaState>()
             .EntityFrameworkRepository(r =>
             {
                 r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                 r.AddDbContext<DbContext, AppDbContext>((sp, o) =>
                 {
                     var cs = sp.GetRequiredService<IConfiguration>()
                                .GetConnectionString("DefaultConnection")!;
                     o.UseNpgsql(cs).UseSnakeCaseNamingConvention();
                 });
             });

            x.AddConsumer<InitiatePaymentConsumer>();
            x.AddConsumer<EnrollmentConfirmedConsumer>();

            // EF Core Outbox — replaces OutboxProcessor + OutboxMessage.
            // Messages published via IPublishEndpoint during a SaveChanges are stored
            // in MassTransit's outbox tables atomically, then delivered to the broker
            // by MassTransit's background delivery service after commit.
            x.AddEntityFrameworkOutbox<AppDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            if (!string.IsNullOrWhiteSpace(rabbitHost))
            {
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Host(rabbitHost, h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });
                    cfg.ConfigureEndpoints(ctx);
                });
            }
            else
            {
                // No broker configured — in-memory transport for local dev without Docker.
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            }
        });

        return services;
    }

    /// <summary>Applies pending EF Core migrations on startup.</summary>
    public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.MigrateAsync().GetAwaiter().GetResult();
        return app;
    }
}
