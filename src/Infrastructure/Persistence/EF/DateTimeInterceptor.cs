using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.EF;

/// <summary>
/// Normalises every DateTime property with Kind == Unspecified to UTC before save.
///
/// PostgreSQL's `timestamp with time zone` column stores UTC, but EF Core will
/// pass a DateTime(Kind=Unspecified) as-is, causing Npgsql to throw or — worse —
/// silently shift the value by the server's local offset.
///
/// This interceptor acts as a persistence-boundary safety net: even if a value
/// enters from an untrusted source without an explicit Kind, it arrives in the
/// database as UTC.
/// </summary>
public class DateTimeInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData      eventData,
        InterceptionResult<int> result)
    {
        Normalise(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData      eventData,
        InterceptionResult<int> result,
        CancellationToken       ct = default)
    {
        Normalise(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private static void Normalise(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        foreach (var property in entry.Properties)
        {
            if (property.CurrentValue is DateTime { Kind: DateTimeKind.Unspecified } dt)
                property.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
    }
}
