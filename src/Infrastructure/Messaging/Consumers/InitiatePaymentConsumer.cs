using Domain.Aggregates.Payment.Events;
using MassTransit;

namespace Infrastructure.Messaging.Consumers;

/// <summary>
/// Simulates a payment service for demo purposes.
/// In production this would be a separate microservice or call to a payment gateway.
///
/// Receives InitiatePayment from the saga → immediately publishes PaymentCompleted.
/// This lets the full GroupJoin flow run end-to-end without a real payment provider.
/// </summary>
public class InitiatePaymentConsumer : IConsumer<InitiatePayment>
{
    public async Task Consume(ConsumeContext<InitiatePayment> context)
    {
        // Real implementation: charge the card, create a Payment aggregate, etc.
        // For demo: auto-complete after a short delay to simulate async payment.
        await Task.Delay(500, context.CancellationToken);

        await context.Publish(new PaymentCompleted(context.Message.PaymentId));
    }
}
