using Domain.Abstractions;
using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;

namespace Infrastructure.Persistence.EF;

/// <summary>
/// EF Core SaveChanges interceptor that converts in-memory domain events into
/// durable OutboxMessages before the transaction commits.
///
/// Flow on SaveChangesAsync:
///   1. Collect all IDomainEvents from tracked Entity instances (PopDomainEvents clears them)
///   2. Serialise each event to JSON with its AssemblyQualifiedName as the type discriminator
///   3. Insert OutboxMessages in the same transaction — guaranteed delivery once the commit succeeds
///   4. OutboxProcessor (BackgroundService) polls and re-publishes via MediatR
/// </summary>
public class DomainEventDispatcherInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData    eventData,
        InterceptionResult<int> result,
        CancellationToken     ct = default)
    {
        Enqueue(eventData.Context!);
        return await base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData    eventData,
        InterceptionResult<int> result)
    {
        Enqueue(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    private static void Enqueue(DbContext context)
    {
        var domainEvents = context.ChangeTracker
            .Entries<Entity>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        if (domainEvents.Count == 0) return;

        var messages = domainEvents.Select(e => new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            EventType = e.GetType().AssemblyQualifiedName!,
            Payload   = JsonConvert.SerializeObject(e),
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        context.Set<OutboxMessage>().AddRange(messages);
    }
}
