using Domain.Abstractions;
using MediatR;

namespace Application.GroupJoinRequests.GetGroupJoinRequest;

// Query handler: delegate to the read-side query service, return Result.
// No SaveChanges, no domain events, no business logic — just a thin projection.
public class Handler : IRequestHandler<Query, Result<GroupJoinRequestDto>>
{
    private readonly IGroupJoinRequestQueryService _queryService;

    public Handler(IGroupJoinRequestQueryService queryService)
        => _queryService = queryService;

    public async Task<Result<GroupJoinRequestDto>> Handle(Query request, CancellationToken ct)
    {
        var dto = await _queryService.GetByIdAsync(request.Id, ct);

        return dto is null
            ? Result<GroupJoinRequestDto>.NotFound()
            : Result<GroupJoinRequestDto>.Success(dto);
    }
}
