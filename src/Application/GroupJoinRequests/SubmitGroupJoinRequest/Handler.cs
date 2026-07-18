using Application.Common.Interfaces;
using Domain.Abstractions;
using Domain.Ids;
using MediatR;

namespace Application.GroupJoinRequests.SubmitGroupJoinRequest;

public class Handler : IRequestHandler<Command, Result<GroupJoinRequestId>>
{
    private readonly IApplicationDbContext _db;

    public Handler(IApplicationDbContext db) => _db = db;

    public async Task<Result<GroupJoinRequestId>> Handle(Command request, CancellationToken ct)
    {
        var joinRequest = Domain.Aggregates.GroupJoinRequest.GroupJoinRequest.Create(
            request.StudentId,
            request.GroupId);

        _db.GroupJoinRequests.Add(joinRequest);
        await _db.SaveChangesAsync(ct);

        return Result<GroupJoinRequestId>.Success(joinRequest.Id);
    }
}
