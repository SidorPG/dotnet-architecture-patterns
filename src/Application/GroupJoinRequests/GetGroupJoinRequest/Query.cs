using Application.Common.Authorization;
using Domain.Abstractions;
using Domain.Ids;
using MediatR;

namespace Application.GroupJoinRequests.GetGroupJoinRequest;

// Query — the read side of CQRS.
// No EF DbContext, no writes, no domain events.
// Authorization: any authenticated user can request (no specific permission).
public record Query(GroupJoinRequestId Id)
    : IRequest<Result<GroupJoinRequestDto>>, IAuthorizedRequest;
