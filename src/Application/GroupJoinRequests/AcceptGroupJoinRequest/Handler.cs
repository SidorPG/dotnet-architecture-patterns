using Application.Common.Interfaces;
using Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.GroupJoinRequests.AcceptGroupJoinRequest;

// Handler contains only coordination logic — no domain rules.
// Domain invariants live in GroupJoinRequest.Accept().
// Auth is already enforced by AuthorizationBehavior before this handler runs.
public class Handler : IRequestHandler<Command, Result>
{
    private readonly IApplicationDbContext _db;

    public Handler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(Command request, CancellationToken ct)
    {
        var joinRequest = await _db.GroupJoinRequests
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);

        if (joinRequest is null)
            return Result.NotFound();

        // State transition + domain event raised inside the aggregate.
        // DomainEventDispatcherInterceptor will write the event to the Outbox on SaveChanges.
        joinRequest.Accept(request.InstructorId, request.AgreedPrice, request.AgreedCurrency);

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
