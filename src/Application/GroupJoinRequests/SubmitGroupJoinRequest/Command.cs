using Domain.Abstractions;
using Domain.Ids;
using MediatR;

namespace Application.GroupJoinRequests.SubmitGroupJoinRequest;

public record Command(StudentId StudentId, GroupId GroupId) : IRequest<Result<GroupJoinRequestId>>;
