using Application.Common.Interfaces;
using Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.EF;

/// <summary>
/// EF Core interceptor that stamps CreatedAt / UpdatedAt on every save.
/// ICurrentUser is injected so the acting user is recorded without any
/// domain entity knowing about HTTP context or identity infrastructure.
///
/// Skips UpdatedAt for entities that are being soft-deleted
/// (SoftDeleteInterceptor already sets UpdatedAt via MarkDeleted).
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;

    public AuditInterceptor(ICurrentUser currentUser)
        => _currentUser = currentUser;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData      eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData      eventData,
        InterceptionResult<int> result,
        CancellationToken       ct = default)
    {
        Apply(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext context)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;
            if (entry.Entity is not AuditableEntity entity) continue;

            if (entry.State == EntityState.Added)
                entity.MarkCreated(now);

            if (entry.State == EntityState.Modified)
            {
                // SoftDeleteInterceptor already called MarkDeleted (which sets UpdatedAt).
                if (entity.IsDeleted) continue;
                entity.MarkUpdated(now);
            }
        }
    }
}
