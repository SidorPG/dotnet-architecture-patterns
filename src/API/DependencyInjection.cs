using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;

namespace API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddControllers(o => o.Conventions.Add(new AuthorizeByRequestConvention()))
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter()));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // JWT is optional — omit Auth:Authority in config to skip validation (demo mode).
        var jwtAuthority = configuration["Auth:Authority"];
        var authBuilder = services.AddAuthentication("Bearer");
        if (!string.IsNullOrWhiteSpace(jwtAuthority))
        {
            authBuilder.AddJwtBearer("Bearer", o =>
            {
                o.Authority = jwtAuthority;
                o.Audience  = configuration["Auth:Audience"];
            });
        }

        services.AddAuthorization();

        return services;
    }
}
