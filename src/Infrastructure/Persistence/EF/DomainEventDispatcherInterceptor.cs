using Domain.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.EF;

/// <summary>
/// EF Core SaveChanges interceptor that publishes domain events via MassTransit
/// before the transaction commits — replacing the manual OutboxMessage approach.
///
/// When UseEntityFrameworkOutbox is configured, IPublishEndpoint writes messages
/// to MassTransit's own outbox tables within the SAME EF Core transaction.
/// After commit, MassTransit's background service delivers them to RabbitMQ.
/// Atomicity guarantee is identical to the manual outbox, but the infrastructure
/// is provided by MassTransit rather than hand-rolled.
///
/// Compare with main branch DomainEventDispatcherInterceptor which writes to
/// the custom OutboxMessage table and relies on OutboxProcessor for delivery.
/// </summary>
public class DomainEventDispatcherInterceptor(IPublishEndpoint publishEndpoint)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData     eventData,
        InterceptionResult<int> result,
        CancellationToken      ct = default)
    {
        await DispatchAsync(eventData.Context!, ct);
        return await base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData     eventData,
        InterceptionResult<int> result)
    {
        DispatchAsync(eventData.Context!, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    private async Task DispatchAsync(DbContext context, CancellationToken ct)
    {
        var events = context.ChangeTracker
            .Entries<Entity>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        foreach (var @event in events)
            await publishEndpoint.Publish((object)@event, @event.GetType(), ct);
    }
}
