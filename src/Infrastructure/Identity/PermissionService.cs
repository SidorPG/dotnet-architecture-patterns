using Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Identity;

/// <summary>
/// Resolves permissions from JWT claims.
/// Each permission string (e.g. "joinrequests:instructor") must appear as a claim
/// on the token — the issuer is responsible for embedding them at login.
///
/// In demo mode DemoAuthenticationHandler parses the Bearer token value as
/// comma-separated claim names, so the same check works without a real OIDC server.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IHttpContextAccessor _accessor;

    public PermissionService(IHttpContextAccessor accessor)
        => _accessor = accessor;

    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct)
    {
        var user = _accessor.HttpContext?.User;
        if (user is null)
            return Task.FromResult(false);

        // Claim type equals the permission string: "joinrequests:instructor", etc.
        var granted = user.HasClaim(c => c.Type == permission);
        return Task.FromResult(granted);
    }
}
