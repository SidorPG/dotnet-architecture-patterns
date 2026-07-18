using Application.Common.Interfaces;
using Application.GroupJoinProcessManagers;
using Application.GroupJoinRequests;
using Infrastructure.Identity;
using Infrastructure.Outbox;
using Infrastructure.Persistence;
using Infrastructure.Persistence.EF;
using Infrastructure.QueryServices;
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
        // ── Interceptors (order matters: each calls base, forming a chain) ──
        services.AddScoped<SoftDeleteInterceptor>();           // 1. mark IsDeleted
        services.AddScoped<AuditInterceptor>();                // 2. stamp CreatedAt/UpdatedAt
        services.AddScoped<DateTimeInterceptor>();             // 3. normalise Kind=Unspecified → UTC
        // DomainEventDispatcherInterceptor has no deps — instantiated inline (4. enqueue events)

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
                    new DomainEventDispatcherInterceptor());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ── Identity ──────────────────────────────────────────────────
        services.AddHttpContextAccessor();

        var jwtAuthority = configuration["Auth:Authority"];
        if (string.IsNullOrWhiteSpace(jwtAuthority))
            // Demo mode: every request is treated as authenticated (no OIDC required).
            services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
        else
            services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddScoped<IPermissionService, PermissionService>();

        // ── Query services ────────────────────────────────────────────
        services.AddScoped<IGroupJoinRequestQueryService, GroupJoinRequestQueryService>();
        services.AddScoped<IGroupJoinProcessManagerQueryService, GroupJoinProcessManagerQueryService>();

        // ── Background services ───────────────────────────────────────
        services.AddHostedService<OutboxProcessor>();

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
