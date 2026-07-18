using Domain.Abstractions;
using Domain.Ids;

namespace Domain.Aggregates.Payment.Events;

// Raised by the Payment aggregate (outside this repo's scope) when a payment succeeds.
// The Outbox relays it to OnPaymentCompleted in the Application layer.
public record PaymentCompleted(PaymentId PaymentId) : IDomainEvent;
