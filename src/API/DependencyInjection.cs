using API.Swagger;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

namespace API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddControllers(o => o.Conventions.Add(new AuthorizeByRequestConvention()))
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter()));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title   = "Dotnet Architecture Patterns",
                Version = "v1",
                Description = "CQRS · MediatR · DDD · Result pattern · Auth pipeline demo"
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Description  = """
                    **Production**: paste a JWT access token from your OIDC provider.

                    **Demo mode** (no Auth:Authority configured): enter a comma-separated list
                    of permission claim names to simulate different roles, e.g.:
                    - `joinrequests:read` — read any request
                    - `joinrequests:student` — submit requests
                    - `joinrequests:instructor` — view pending + accept
                    - `joinrequests:instructor,joinrequests:read` — combine freely

                    No Authorization header → **401**. Wrong claims → **403**.
                    """
            };
            c.AddSecurityDefinition("Bearer", scheme);
            c.OperationFilter<AuthorizationOperationFilter>();
        });

        var jwtAuthority = configuration["Auth:Authority"];
        var authBuilder  = services.AddAuthentication("Bearer");

        if (!string.IsNullOrWhiteSpace(jwtAuthority))
        {
            // Production: validate JWT signature, expiry, and audience via OIDC discovery.
            authBuilder.AddScheme<JwtAuthenticationOptions, JwtAuthenticationHandler>("Bearer", o =>
            {
                o.Authority = jwtAuthority;
                o.Audience  = configuration["Auth:Audience"] ?? string.Empty;
            });
        }
        else if (environment.IsDevelopment())
        {
            // Development demo: token value = comma-separated permission claim names.
            authBuilder.AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
                "Bearer", _ => { });
        }

        services.AddAuthorization();

        return services;
    }
}
