using Application.Common.Interfaces;
using Domain.Abstractions;
using Domain.Aggregates.GroupJoinRequest;
using Domain.Ids;
using Infrastructure.Messaging;
using Infrastructure.Persistence.EF;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<GroupJoinRequest>  GroupJoinRequests  => Set<GroupJoinRequest>();

    // Saga state — replaces the manual GroupJoinProcessManager aggregate.
    public DbSet<GroupJoinSagaState> GroupJoinSagaStates => Set<GroupJoinSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureGroupJoinRequest(modelBuilder);
        ConfigureGroupJoinSagaState(modelBuilder);

        // MassTransit outbox and inbox tracking tables.
        // These replace our manual OutboxMessage / OutboxProcessor.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyGlobalFilter<AuditableEntity>(e => !e.IsDeleted);
    }

    private static void ConfigureGroupJoinRequest(ModelBuilder b)
    {
        b.Entity<GroupJoinRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
             .HasConversion(v => v.Value, v => new GroupJoinRequestId(v));

            e.Property(x => x.StudentId)
             .HasConversion(v => v.Value, v => new StudentId(v));

            e.Property(x => x.GroupId)
             .HasConversion(v => v.Value, v => new GroupId(v));

            e.Property(x => x.ReviewedBy)
             .HasConversion(
                 new ValueConverter<InstructorId, Guid>(v => v.Value, v => new InstructorId(v)));
        });
    }

    private static void ConfigureGroupJoinSagaState(ModelBuilder b)
    {
        b.Entity<GroupJoinSagaState>(e =>
        {
            e.HasKey(x => x.CorrelationId);
            e.HasIndex(x => x.PaymentId);  // needed for CorrelateBy(saga.PaymentId, ...)
        });
    }
}
