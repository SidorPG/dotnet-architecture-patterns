using Domain.Aggregates.GroupJoinRequest;
using Domain.Aggregates.GroupJoinProcessManager;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

/// <summary>
/// Application-layer view of the database context.
/// Infrastructure implements this; Application depends only on the interface.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<GroupJoinRequest>        GroupJoinRequests        { get; }
    DbSet<GroupJoinProcessManager> GroupJoinProcessManagers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
