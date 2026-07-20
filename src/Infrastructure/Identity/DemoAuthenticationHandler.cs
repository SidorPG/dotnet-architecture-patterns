using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Infrastructure.Identity;

/// <summary>
/// Authentication handler for local demo / docker-compose without an OIDC server.
/// Registered as the "Bearer" scheme when Auth:Authority is not configured.
///
/// Token format: comma-separated permission claim names, e.g.
///   "joinrequests:instructor"
///   "joinrequests:student,joinrequests:read"
///
/// No Authorization header  →  401 Unauthorized  (access denied at HTTP layer)
/// Wrong permission claims  →  403 Forbidden     (AuthorizationBehavior in MediatR)
/// Correct claims           →  request proceeds
///
/// Never register this in production.
/// </summary>
public class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    // Fixed demo identity so ICurrentUser.UserId returns a stable non-empty Guid.
    public static readonly Guid DemoUserId = new("00000000-0000-0000-0000-000000000001");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
            return Task.FromResult(AuthenticateResult.NoResult()); // → 401

        // Strip "Bearer " prefix; the remainder is the demo permission list.
        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : authHeader.Trim();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, DemoUserId.ToString()),
            new(ClaimTypes.Name,           "demo-user"),
        };

        // Each comma-separated segment becomes a permission claim checked by PermissionService.
        foreach (var perm in token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(perm, "true"));

        var identity  = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
