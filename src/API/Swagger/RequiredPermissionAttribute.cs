namespace API.Swagger;

/// <summary>
/// Documents the specific JWT claim required by an endpoint.
/// Read by <see cref="AuthorizationOperationFilter"/> to surface the
/// permission in Swagger UI and generate accurate 403 response descriptions.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiredPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
