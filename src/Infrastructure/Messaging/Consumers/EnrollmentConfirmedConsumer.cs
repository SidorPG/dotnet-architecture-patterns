using Application.Common.Interfaces;
using MassTransit;

namespace Infrastructure.Messaging.Consumers;

/// <summary>
/// Handles EnrollmentConfirmed (published by GroupJoinStateMachine when payment succeeds).
/// Replaces the MediatR OnPaymentCompleted notification handler.
///
/// Confirms the GroupJoinRequest aggregate — the side-effects that previously
/// lived in OnPaymentCompleted now live here, triggered by the saga outcome.
/// </summary>
public class EnrollmentConfirmedConsumer : IConsumer<EnrollmentConfirmed>
{
    private readonly IApplicationDbContext _db;

    public EnrollmentConfirmedConsumer(IApplicationDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<EnrollmentConfirmed> context)
    {
        var msg = context.Message;

        var joinRequest = await _db.GroupJoinRequests
            .FindAsync([msg.RequestId], context.CancellationToken);

        if (joinRequest is null) return;

        joinRequest.Confirm();
        await _db.SaveChangesAsync(context.CancellationToken);
    }
}
