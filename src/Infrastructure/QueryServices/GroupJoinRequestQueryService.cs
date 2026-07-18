using Application.GroupJoinRequests;
using Domain.Aggregates.GroupJoinRequest;
using Domain.Ids;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.QueryServices;

public class GroupJoinRequestQueryService : IGroupJoinRequestQueryService
{
    private readonly AppDbContext _db;

    public GroupJoinRequestQueryService(AppDbContext db) => _db = db;

    public async Task<GroupJoinRequestDto?> GetByIdAsync(GroupJoinRequestId id, CancellationToken ct = default)
    {
        return await _db.GroupJoinRequests
            .Where(r => r.Id == id)
            .Select(r => new GroupJoinRequestDto(
                r.Id.Value,
                r.StudentId.Value,
                r.GroupId.Value,
                r.Status,
                r.RequestedAt,
                r.AgreedPrice,
                r.AgreedCurrency))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<GroupJoinRequestDto>> GetPendingAsync(CancellationToken ct = default)
    {
        return await _db.GroupJoinRequests
            .Where(r => r.Status == Domain.Aggregates.GroupJoinRequest.JoinRequestStatus.PendingApproval)
            .Select(r => new GroupJoinRequestDto(
                r.Id.Value,
                r.StudentId.Value,
                r.GroupId.Value,
                r.Status,
                r.RequestedAt,
                r.AgreedPrice,
                r.AgreedCurrency))
            .ToListAsync(ct);
    }
}
