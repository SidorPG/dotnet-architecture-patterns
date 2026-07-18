using Application.Common.Interfaces;

namespace Infrastructure.Identity;

// Used when Auth:Authority is not configured (local demo / docker compose without OIDC).
// Makes every request appear authenticated so the pipeline runs end-to-end.
// Never register this in production — it bypasses all JWT validation.
public class AnonymousCurrentUser : ICurrentUser
{
    public bool  IsAuthenticated => true;
    public Guid? UserId          => Guid.Empty;
}
