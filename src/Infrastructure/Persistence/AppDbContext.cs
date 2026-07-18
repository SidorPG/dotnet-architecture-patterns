using Application.Common.Interfaces;
using Domain.Abstractions;
using Domain.Aggregates.GroupJoinProcessManager;
using Domain.Aggregates.GroupJoinRequest;
using Infrastructure.Outbox;
using Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<GroupJoinRequest>        GroupJoinRequests        => Set<GroupJoinRequest>();
    public DbSet<GroupJoinProcessManager> GroupJoinProcessManagers => Set<GroupJoinProcessManager>();
    public DbSet<OutboxMessage>           OutboxMessages           => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft-delete filter applied to every AuditableEntity root in the model.
        modelBuilder.ApplyGlobalFilter<AuditableEntity>(e => !e.IsDeleted);
    }
}
