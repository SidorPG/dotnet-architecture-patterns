using Application.Common.Authorization;
using Domain.Abstractions;
using Domain.Ids;
using MediatR;

namespace Application.GroupJoinRequests.GetGroupJoinRequest;

// Query — the read side of CQRS.
// No EF DbContext, no writes, no domain events.
public record Query(GroupJoinRequestId Id)
    : IRequest<Result<GroupJoinRequestDto>>, IAuthorizedRequest
{
    public string? RequiredPermission => Permissions.JoinRequests.Read;
}
