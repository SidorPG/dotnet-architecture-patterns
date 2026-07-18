using Domain.Ids;

namespace Application.GroupJoinRequests;

/// <summary>
/// Read-side contract for join request queries.
/// Implemented in Infrastructure (Dapper or EF readonly projections).
/// Application layer depends only on this interface — no EF leaking upward.
/// </summary>
public interface IGroupJoinRequestQueryService
{
    Task<GroupJoinRequestDto?> GetByIdAsync(GroupJoinRequestId id, CancellationToken ct = default);
    Task<IReadOnlyList<GroupJoinRequestDto>> GetPendingAsync(CancellationToken ct = default);
}
