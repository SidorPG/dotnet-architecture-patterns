using Application.Common.Interfaces;

namespace Infrastructure.Identity;

// Stub: reads permission grants from the database or an in-process policy store.
// Full implementation would query a permissions table built from user roles.
public class PermissionService : IPermissionService
{
    public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct)
    {
        // Replace with real policy evaluation.
        return Task.FromResult(true);
    }
}
