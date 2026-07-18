using Application.Common.Interfaces;
using Domain.Aggregates.GroupJoinProcessManager;
using Domain.Aggregates.Payment.Events;
using MediatR;

namespace Application.GroupJoinProcessManagers.Notifications;

/// <summary>
/// Listens for PaymentCompleted (raised by the payment aggregate, routed via Outbox).
/// When payment is confirmed, this handler:
///   1. Finds the process manager by payment ID
///   2. Confirms the join request
///   3. Assigns the student to the group
///   4. Marks the PM completed
///
/// All three writes happen in a single SaveChanges — atomic by default with EF Core.
/// </summary>
public class OnPaymentCompleted : INotificationHandler<PaymentCompleted>
{
    private readonly IApplicationDbContext             _db;
    private readonly IGroupJoinProcessManagerQueryService _pmQuery;

    public OnPaymentCompleted(IApplicationDbContext db, IGroupJoinProcessManagerQueryService pmQuery)
    {
        _db      = db;
        _pmQuery = pmQuery;
    }

    public async Task Handle(PaymentCompleted notification, CancellationToken ct)
    {
        var pm = await _pmQuery.FindByPaymentIdAsync(notification.PaymentId, ct);
        if (pm is null)                                  return;
        if (pm.State == ProcessManagerState.Completed)   return;

        var joinRequest = await _db.GroupJoinRequests.FindAsync([pm.GroupJoinRequestId], ct);
        if (joinRequest is null) return;

        var student = await _db.Students.FindAsync([pm.StudentId], ct);
        if (student is null) return;

        joinRequest.Confirm();
        student.AssignToGroup(pm.GroupId.Value);
        pm.MarkCompleted();

        await _db.SaveChangesAsync(ct);
    }
}
