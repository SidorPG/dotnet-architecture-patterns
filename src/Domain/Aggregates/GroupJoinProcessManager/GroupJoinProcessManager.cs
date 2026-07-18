using Domain.Abstractions;
using Domain.Ids;

namespace Domain.Aggregates.GroupJoinProcessManager;

/// <summary>
/// Tracks the lifecycle of a student joining a group:
///   Created → AwaitingPayment → Completed
///                            ↘ Compensating → Compensated  (on rejection / cancellation)
///
/// Kept separate from GroupJoinRequest so the orchestration state doesn't
/// pollute the core domain aggregate.
/// </summary>
public class GroupJoinProcessManager : AuditableEntity<ProcessManagerId>
{
    private GroupJoinProcessManager() : base(default) { }

    private GroupJoinProcessManager(
        ProcessManagerId       id,
        GroupJoinRequestId     joinRequestId,
        StudentId              studentId,
        GroupId                groupId) : base(id)
    {
        GroupJoinRequestId = joinRequestId;
        StudentId          = studentId;
        GroupId            = groupId;
        State              = ProcessManagerState.Created;
    }

    public GroupJoinRequestId GroupJoinRequestId { get; private set; }
    public PaymentId?         PaymentId          { get; private set; }
    public StudentId          StudentId          { get; private set; }
    public GroupId            GroupId            { get; private set; }
    public ProcessManagerState State             { get; private set; }

    public static GroupJoinProcessManager Create(
        GroupJoinRequestId joinRequestId,
        StudentId          studentId,
        GroupId            groupId)
        => new(new ProcessManagerId(Guid.NewGuid()), joinRequestId, studentId, groupId);

    public void StartAwaitingPayment(PaymentId paymentId)
    {
        if (State != ProcessManagerState.Created)
            throw new InvalidOperationException("PM must be in Created state to start awaiting payment.");

        PaymentId = paymentId;
        State     = ProcessManagerState.AwaitingPayment;
    }

    public void MarkCompleted()
    {
        if (State == ProcessManagerState.Completed) return;
        if (State is not (ProcessManagerState.AwaitingPayment or ProcessManagerState.Completing))
            throw new InvalidOperationException("PM must be AwaitingPayment or Completing to complete.");

        State = ProcessManagerState.Completed;
    }

    public void StartCompensation()
    {
        if (State is ProcessManagerState.Compensating or ProcessManagerState.Compensated) return;
        State = ProcessManagerState.Compensating;
    }

    public void MarkCompensated()
    {
        if (State != ProcessManagerState.Compensating)
            throw new InvalidOperationException("PM must be Compensating to mark as compensated.");

        State = ProcessManagerState.Compensated;
    }
}
