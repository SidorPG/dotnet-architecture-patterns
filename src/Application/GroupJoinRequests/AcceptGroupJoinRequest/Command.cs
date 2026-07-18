using Application.Common.Authorization;
using Domain.Abstractions;
using Domain.Ids;
using MediatR;

namespace Application.GroupJoinRequests.AcceptGroupJoinRequest;

// Command is a plain record — no logic, no dependencies.
// InstructorId is resolved by the API controller from the JWT claim before dispatching.
// Authorization requirement is declared inline via IAuthorizedRequest.
public record Command(
    GroupJoinRequestId RequestId,
    InstructorId       InstructorId,
    decimal            AgreedPrice,
    string             AgreedCurrency
) : IRequest<Result>, IAuthorizedRequest
{
    public string? RequiredPermission => Permissions.JoinRequests.InstructorWrite;
}
