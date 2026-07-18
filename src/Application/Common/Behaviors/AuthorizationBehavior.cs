using Application.Common.Authorization;
using Application.Common.Interfaces;
using MediatR;

namespace Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that fires before every command or query
/// implementing <see cref="IAuthorizedRequest"/>.
///
/// Pipeline order (registered in DI):
///   [request] → AuthorizationBehavior → ValidationBehavior → Handler
///
/// Auth failures throw exceptions that the API middleware maps to 401 / 403.
/// Invisible resources (e.g. wrong tenant) return 404 from the handler itself.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuthorizedRequest
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissionService;

    public AuthorizationBehavior(ICurrentUser currentUser, IPermissionService permissionService)
    {
        _currentUser       = currentUser;
        _permissionService = permissionService;
    }

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        if (request.RequiredPermission is { } permission)
        {
            var allowed = await _permissionService
                .HasPermissionAsync(_currentUser.UserId!.Value, permission, ct);

            if (!allowed)
                throw new ForbiddenException();
        }

        return await next();
    }
}

public class ForbiddenException : Exception;
