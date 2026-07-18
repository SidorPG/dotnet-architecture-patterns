using Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Identity;

/// <summary>
/// Resolves permissions from JWT claims.
/// Each permission string (e.g. "joinrequests:instructor") must appear as a claim
/// on the token — the issuer is responsible for embedding them at login.
///
/// Demo / anonymous mode: when UserId is Guid.Empty (AnonymousCurrentUser),
/// all permissions are granted so the pipeline runs end-to-end without OIDC.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IHttpContextAccessor _accessor;

    public PermissionService(IHttpContextAccessor accessor)
        => _accessor = accessor;

    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct)
    {
        // Guid.Empty is the sentinel for AnonymousCurrentUser (demo mode).
        if (userId == Guid.Empty)
            return Task.FromResult(true);

        var user = _accessor.HttpContext?.User;
        if (user is null)
            return Task.FromResult(false);

        // Claim type equals the permission string: "joinrequests:instructor", etc.
        var granted = user.HasClaim(c => c.Type == permission);
        return Task.FromResult(granted);
    }
}
