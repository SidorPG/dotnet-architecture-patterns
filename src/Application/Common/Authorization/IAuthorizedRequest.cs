namespace Application.Common.Authorization;

/// <summary>
/// Marks a command or query as requiring authorization.
/// Implement on any IRequest that needs authentication or permission checks.
/// Null <see cref="RequiredPermission"/> means authentication-only (no specific permission).
/// </summary>
public interface IAuthorizedRequest
{
    string? RequiredPermission => null;
}
