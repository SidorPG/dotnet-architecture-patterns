using Application.Common.Authorization;
using Domain.Abstractions;
using MediatR;

namespace Application.GroupJoinRequests.GetPendingGroupJoinRequests;

// Returns all pending-approval requests — instructor-only read.
public record Query : IRequest<Result<IReadOnlyList<GroupJoinRequestDto>>>, IAuthorizedRequest
{
    public string? RequiredPermission => Permissions.JoinRequests.InstructorWrite;
}
