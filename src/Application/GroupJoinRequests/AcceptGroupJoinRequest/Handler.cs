using Application.Common.Interfaces;
using Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.GroupJoinRequests.AcceptGroupJoinRequest;

// Handler contains only coordination logic — no domain rules.
// Domain invariants live in GroupJoinRequest.Accept().
public class Handler : IRequestHandler<Command, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser          _currentUser;
    private readonly IInstructorQueryService _instructors;

    public Handler(IApplicationDbContext db, ICurrentUser currentUser, IInstructorQueryService instructors)
    {
        _db          = db;
        _currentUser = currentUser;
        _instructors = instructors;
    }

    public async Task<Result> Handle(Command request, CancellationToken ct)
    {
        var instructor = await _instructors.GetByExternalIdAsync(_currentUser.UserId!.Value.ToString(), ct);
        if (instructor is null)
            return Result.Failure("Instructor not found");

        var joinRequest = await _db.GroupJoinRequests
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);
        if (joinRequest is null)
            return Result.Failure("Join request not found");

        var group = await _db.Groups.FindAsync([joinRequest.GroupId], ct);
        if (group is null)
            return Result.Failure("Group not found");

        if (group.InstructorId != instructor.Id && group.TheoryInstructorId != instructor.Id)
            return Result.Failure("Access denied");

        // State transition + domain event raised inside the aggregate.
        // DomainEventDispatcherInterceptor will write the event to the Outbox on SaveChanges.
        joinRequest.Accept(instructor.Id, request.AgreedPrice, request.AgreedCurrency);

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
