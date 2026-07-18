using Domain.Abstractions;
using MediatR;

namespace Application.GroupJoinRequests.GetPendingGroupJoinRequests;

public record Query : IRequest<Result<IReadOnlyList<GroupJoinRequestDto>>>;
