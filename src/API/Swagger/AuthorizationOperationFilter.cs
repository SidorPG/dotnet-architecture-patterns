using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace API.Swagger;

/// <summary>
/// Swagger operation filter that wires authorization metadata into the generated spec:
///
/// 1. Detects [Authorize] on the action → adds Bearer security requirement (padlock icon)
///    and ensures 401 Unauthorized is listed in the response table.
///
/// 2. Detects [RequiredPermission] → surfaces the specific claim name in the description
///    and adds a 403 Forbidden response with the claim name.
///
/// This makes the authorization contract visible without duplicating it in
/// XML doc comments or hand-editing the OpenAPI spec.
/// </summary>
public class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.ApiDescription.ActionDescriptor.FilterDescriptors
            .Any(f => f.Filter is AuthorizeFilter or IAuthorizeData);

        if (!hasAuthorize)
            return;

        // Padlock icon in Swagger UI — signals that a Bearer token is required.
        var bearerRef = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        operation.Security.Add(new OpenApiSecurityRequirement { [bearerRef] = [] });

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Unauthorized — missing or invalid JWT"
        });

        // Surface the specific permission claim if declared via [RequiredPermission].
        var permission = context.MethodInfo.GetCustomAttribute<RequiredPermissionAttribute>();
        if (permission is null)
            return;

        var note = $"Requires JWT claim: `{permission.Permission}`";
        operation.Description = string.IsNullOrEmpty(operation.Description)
            ? note
            : $"{operation.Description}  \n{note}";

        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = $"Forbidden — JWT lacks the `{permission.Permission}` claim"
        });
    }
}
