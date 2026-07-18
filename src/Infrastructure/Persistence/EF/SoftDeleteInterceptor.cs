using Application.Common.Interfaces;
using Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.EF;

/// <summary>
/// EF Core interceptor that converts physical deletes into soft-deletes.
/// When EF tracks an entity as Deleted, this interceptor calls MarkDeleted()
/// and resets the state to Modified — the entity is never actually removed from the DB.
///
/// The domain entity has no EF Core knowledge; all infrastructure concerns stay here.
/// </summary>
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;

    public SoftDeleteInterceptor(ICurrentUser currentUser)
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
            if (entry.State != EntityState.Deleted) continue;
            if (entry.Entity is not AuditableEntity entity) continue;

            entity.MarkDeleted(now);
            entry.State = EntityState.Modified;   // ← redirect: no DELETE SQL
        }
    }
}
