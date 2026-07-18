using Application.GroupJoinProcessManagers;
using Domain.Aggregates.GroupJoinProcessManager;
using Domain.Ids;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.QueryServices;

public class GroupJoinProcessManagerQueryService : IGroupJoinProcessManagerQueryService
{
    private readonly AppDbContext _db;

    public GroupJoinProcessManagerQueryService(AppDbContext db) => _db = db;

    public async Task<GroupJoinProcessManager?> FindByPaymentIdAsync(PaymentId paymentId, CancellationToken ct)
    {
        return await _db.GroupJoinProcessManagers
            .FirstOrDefaultAsync(pm => pm.PaymentId == paymentId, ct);
    }
}
