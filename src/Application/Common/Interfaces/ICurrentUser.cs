namespace Application.Common.Interfaces;

// Fulfilled by Infrastructure via JWT token context — decouples domain from auth mechanism.
public interface ICurrentUser
{
    bool  IsAuthenticated { get; }
    Guid? UserId          { get; }
}

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct);
}
