using Domain.Aggregates.GroupJoinRequest.Events;
using Domain.Aggregates.Payment.Events;
using Domain.Ids;
using MassTransit;

namespace Infrastructure.Messaging;

/// <summary>
/// MassTransit StateMachine Saga — replaces the manual GroupJoinProcessManager.
///
/// The state machine tracks the group-join flow across three aggregates
/// (GroupJoinRequest → Payment → Student) without coupling them directly.
/// MassTransit persists GroupJoinSagaState to the database between messages,
/// so the saga survives restarts and handles out-of-order delivery.
///
/// Correlation strategy:
///   GroupJoinRequestSubmitted / GroupJoinRequestAccepted  →  CorrelateById (RequestId)
///   PaymentCompleted                                       →  CorrelateBy(saga.PaymentId)
/// </summary>
public class GroupJoinStateMachine : MassTransitStateMachine<GroupJoinSagaState>
{
    // ── States ────────────────────────────────────────────────────────
    public State Submitted       { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State Completed       { get; private set; } = null!;

    // ── Events ────────────────────────────────────────────────────────
    public Event<GroupJoinRequestSubmitted> RequestSubmitted { get; private set; } = null!;
    public Event<GroupJoinRequestAccepted>  RequestAccepted  { get; private set; } = null!;
    public Event<PaymentCompleted>          PaymentDone      { get; private set; } = null!;

    public GroupJoinStateMachine()
    {
        InstanceState(x => x.CurrentState);

        // Messages in this flow all carry RequestId — simple CorrelateById.
        Event(() => RequestSubmitted, e => e.CorrelateById(m => m.Message.RequestId.Value));
        Event(() => RequestAccepted,  e => e.CorrelateById(m => m.Message.RequestId.Value));

        // PaymentCompleted carries PaymentId, not RequestId.
        // CorrelateBy does a DB lookup: find the saga whose PaymentId matches.
        Event(() => PaymentDone, e =>
            e.CorrelateBy<Guid>(state => state.PaymentId, ctx => ctx.Message.PaymentId.Value));

        // ── Transitions ───────────────────────────────────────────────

        Initially(
            When(RequestSubmitted)
                .Then(ctx =>
                {
                    ctx.Saga.StudentId = ctx.Message.StudentId.Value;
                    ctx.Saga.GroupId   = ctx.Message.GroupId.Value;
                })
                .TransitionTo(Submitted));

        During(Submitted,
            When(RequestAccepted)
                .Then(ctx =>
                {
                    // Generate a PaymentId so we can correlate PaymentCompleted later.
                    // In production this would come from a real payment service response.
                    ctx.Saga.PaymentId = NewId.NextGuid();
                })
                // Publish a command to the payment service (or a simulated consumer).
                .PublishAsync(ctx => ctx.Init<InitiatePayment>(new InitiatePayment(
                    new GroupJoinRequestId(ctx.Saga.CorrelationId),
                    new PaymentId(ctx.Saga.PaymentId))))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentDone)
                // Publish a notification that downstream consumers (confirm enrollment etc.) handle.
                .PublishAsync(ctx => ctx.Init<EnrollmentConfirmed>(new EnrollmentConfirmed(
                    new GroupJoinRequestId(ctx.Saga.CorrelationId),
                    new StudentId(ctx.Saga.StudentId),
                    new GroupId(ctx.Saga.GroupId))))
                .TransitionTo(Completed)
                .Finalize());

        // Remove completed saga instances from the DB (optional — keep for audit instead).
        SetCompletedWhenFinalized();
    }
}

// ── Integration messages (saga-internal commands/events) ─────────────────────

/// <summary>Command published by the saga to initiate payment.</summary>
public record InitiatePayment(GroupJoinRequestId RequestId, PaymentId PaymentId);

/// <summary>
/// Event published by the saga when the full flow completes.
/// Downstream consumers handle side-effects (confirm join request, assign student to group).
/// </summary>
public record EnrollmentConfirmed(GroupJoinRequestId RequestId, StudentId StudentId, GroupId GroupId);
