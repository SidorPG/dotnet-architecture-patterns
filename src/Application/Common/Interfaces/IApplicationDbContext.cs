using Domain.Aggregates.GroupJoinRequest;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

/// <summary>
/// Application-layer view of the database context.
/// Infrastructure implements this; Application depends only on the interface.
///
/// Note: GroupJoinProcessManagers removed on feature/masstransit branch —
/// the process manager role is now handled by GroupJoinStateMachine (MassTransit saga).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<GroupJoinRequest> GroupJoinRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
