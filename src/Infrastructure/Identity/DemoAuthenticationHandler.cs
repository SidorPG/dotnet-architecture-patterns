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
/// Authenticates every request with a synthetic anonymous identity so that
/// [Authorize] on controllers passes — actual permission checks are still
/// enforced by AuthorizationBehavior in the MediatR pipeline.
///
/// Never register this in production.
/// </summary>
public class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity  = new ClaimsIdentity([new Claim(ClaimTypes.Name, "demo")], Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
