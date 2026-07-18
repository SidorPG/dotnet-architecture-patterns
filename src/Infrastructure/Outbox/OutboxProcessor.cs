using Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Infrastructure.Outbox;

/// <summary>
/// Background service that polls the outbox table every 10 seconds,
/// deserialises each pending OutboxMessage back into its IDomainEvent type,
/// and publishes it via MediatR so any registered INotificationHandler picks it up.
///
/// Retry logic: up to 5 attempts per message; errors are stored alongside the message
/// for observability without blocking the rest of the batch.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private static readonly TimeSpan Interval  = TimeSpan.FromSeconds(10);
    private const           int      MaxRetries = 5;
    private const           int      BatchSize  = 50;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope     = _scopeFactory.CreateScope();
        var db              = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher       = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await db.OutboxMessages
            .Where(m => m.SentAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType is null)
                {
                    _logger.LogWarning("Unknown domain event type: {EventType}", message.EventType);
                    message.RetryCount = MaxRetries;
                    continue;
                }

                var domainEvent = (IDomainEvent)JsonConvert.DeserializeObject(message.Payload, eventType)!;
                await publisher.Publish(domainEvent, ct);
                message.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {Id} ({EventType})",
                    message.Id, message.EventType);
                message.RetryCount++;
                message.Error = ex.Message;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
