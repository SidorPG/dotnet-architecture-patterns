using Application.Common.Authorization;
using Domain.Abstractions;
using Domain.Ids;
using MediatR;

namespace Application.GroupJoinRequests.SubmitGroupJoinRequest;

public record Command(StudentId StudentId, GroupId GroupId)
    : IRequest<Result<GroupJoinRequestId>>, IAuthorizedRequest
{
    // Any authenticated student can submit — no elevated permission required beyond login.
    public string? RequiredPermission => Permissions.JoinRequests.StudentWrite;
}
