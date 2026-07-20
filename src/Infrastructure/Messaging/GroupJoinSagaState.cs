using MassTransit;

namespace Infrastructure.Messaging;

/// <summary>
/// Durable state of the GroupJoin saga, persisted to the database between messages.
/// Replaces the manual GroupJoinProcessManager domain aggregate.
///
/// CorrelationId = GroupJoinRequestId — all messages in the flow carry this ID
/// so the saga engine can find the right instance.
/// </summary>
public class GroupJoinSagaState : SagaStateMachineInstance
{
    public Guid   CorrelationId { get; set; }   // = GroupJoinRequestId.Value
    public string CurrentState  { get; set; } = null!;

    // Populated on GroupJoinRequestSubmitted
    public Guid StudentId { get; set; }
    public Guid GroupId   { get; set; }

    // Populated when the saga initiates a payment (GroupJoinRequestAccepted handler)
    // Used to correlate the incoming PaymentCompleted message back to this saga instance.
    public Guid PaymentId { get; set; }
}
