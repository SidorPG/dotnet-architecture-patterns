using Domain.Abstractions;
using MediatR;

namespace Application.GroupJoinRequests.GetPendingGroupJoinRequests;

public class Handler : IRequestHandler<Query, Result<IReadOnlyList<GroupJoinRequestDto>>>
{
    private readonly IGroupJoinRequestQueryService _queryService;

    public Handler(IGroupJoinRequestQueryService queryService)
        => _queryService = queryService;

    public async Task<Result<IReadOnlyList<GroupJoinRequestDto>>> Handle(Query request, CancellationToken ct)
    {
        var list = await _queryService.GetPendingAsync(ct);
        return Result<IReadOnlyList<GroupJoinRequestDto>>.Success(list);
    }
}
