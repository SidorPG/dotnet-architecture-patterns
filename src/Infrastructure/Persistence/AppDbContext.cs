using Application.Common.Interfaces;
using Domain.Abstractions;
using Domain.Aggregates.GroupJoinProcessManager;
using Domain.Aggregates.GroupJoinRequest;
using Domain.Ids;
using Infrastructure.Outbox;
using Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        ConfigureGroupJoinRequest(modelBuilder);
        ConfigureGroupJoinProcessManager(modelBuilder);
        ConfigureOutboxMessage(modelBuilder);

        // Soft-delete filter applied to every AuditableEntity root in the model.
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

            // Nullable — EF Core wraps the converter to handle NULL columns automatically.
            e.Property(x => x.ReviewedBy)
             .HasConversion(
                 new ValueConverter<InstructorId, Guid>(v => v.Value, v => new InstructorId(v)));
        });
    }

    private static void ConfigureGroupJoinProcessManager(ModelBuilder b)
    {
        b.Entity<GroupJoinProcessManager>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
             .HasConversion(v => v.Value, v => new ProcessManagerId(v));

            e.Property(x => x.GroupJoinRequestId)
             .HasConversion(v => v.Value, v => new GroupJoinRequestId(v));

            e.Property(x => x.StudentId)
             .HasConversion(v => v.Value, v => new StudentId(v));

            e.Property(x => x.GroupId)
             .HasConversion(v => v.Value, v => new GroupId(v));

            // Nullable PaymentId.
            e.Property(x => x.PaymentId)
             .HasConversion(
                 new ValueConverter<PaymentId, Guid>(v => v.Value, v => new PaymentId(v)));
        });
    }

    private static void ConfigureOutboxMessage(ModelBuilder b)
    {
        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });
    }
}
