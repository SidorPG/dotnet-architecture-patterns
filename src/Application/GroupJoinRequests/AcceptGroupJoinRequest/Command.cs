using Application.Common.Authorization;
using Domain.Abstractions;
using Domain.Ids;
using MediatR;

namespace Application.GroupJoinRequests.AcceptGroupJoinRequest;

// Command is a plain record — no logic, no dependencies.
// Authorization requirement is declared inline via IAuthorizedRequest.
public record Command(
    GroupJoinRequestId RequestId,
    decimal            AgreedPrice,
    string             AgreedCurrency
) : IRequest<Result>, IAuthorizedRequest
{
    public string? RequiredPermission => Permissions.JoinRequests.InstructorWrite;
}
