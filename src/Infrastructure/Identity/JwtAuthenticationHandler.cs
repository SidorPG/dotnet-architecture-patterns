using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Infrastructure.Identity;

/// <summary>
/// Production JWT authentication handler.
/// Counterpart to <see cref="DemoAuthenticationHandler"/> — same structural contract,
/// real validation instead of claim parsing.
///
/// Flow:
///   1. Extract Bearer token from Authorization header.
///   2. Fetch the OIDC discovery document (/.well-known/openid-configuration) to get
///      the signing keys — ConfigurationManager caches and refreshes them automatically.
///   3. Validate token signature, expiry, issuer, and audience via JwtSecurityTokenHandler.
///   4. Build ClaimsPrincipal from the validated token's claims.
///
/// In production replace AddJwtBearer with this handler only if you need to intercept
/// claims transformation or add custom pre-validation logic; otherwise AddJwtBearer
/// provides the same pipeline with less boilerplate.
/// </summary>
public class JwtAuthenticationHandler(
    IOptionsMonitor<JwtAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<JwtAuthenticationOptions>(options, logger, encoder)
{
    private static readonly JwtSecurityTokenHandler _tokenHandler = new();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
            return AuthenticateResult.NoResult(); // → 401

        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : null;

        if (token is null)
            return AuthenticateResult.NoResult();

        try
        {
            var signingKeys = await FetchSigningKeysAsync(Options.Authority, Context.RequestAborted);

            var parameters = new TokenValidationParameters
            {
                ValidIssuer      = Options.Authority,
                ValidAudience    = Options.Audience,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
            };

            var principal = _tokenHandler.ValidateToken(token, parameters, out _);
            var ticket    = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        catch (SecurityTokenException ex)
        {
            return AuthenticateResult.Fail(ex);
        }
    }

    // Fetches JWKS from the OIDC discovery document.
    // ConfigurationManager<OpenIdConnectConfiguration> caches the result and
    // transparently refreshes signing keys when the provider rotates them.
    private static async Task<IEnumerable<SecurityKey>> FetchSigningKeysAsync(
        string authority,
        CancellationToken ct)
    {
        var discoveryUrl = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        var manager      = new ConfigurationManager<OpenIdConnectConfiguration>(
            discoveryUrl,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

        var config = await manager.GetConfigurationAsync(ct);
        return config.SigningKeys;
    }
}

/// <summary>Options for <see cref="JwtAuthenticationHandler"/>.</summary>
public class JwtAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience  { get; set; } = string.Empty;
}
