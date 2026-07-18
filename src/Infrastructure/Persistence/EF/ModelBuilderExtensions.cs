using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Infrastructure.Persistence.EF;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies a global query filter to every entity that inherits from TBase.
    ///
    /// Standard usage — soft-delete filter applied in OnModelCreating:
    ///   modelBuilder.ApplyGlobalFilter&lt;AuditableEntity&gt;(e => !e.IsDeleted);
    ///
    /// TPH note: EF Core only allows a query filter on the root of a TPH hierarchy.
    /// The guard `entityType.BaseType != null` skips derived types automatically,
    /// preventing "Cannot use HasQueryFilter on a non-root entity type" exceptions.
    /// </summary>
    public static void ApplyGlobalFilter<TBase>(
        this ModelBuilder                   modelBuilder,
        Expression<Func<TBase, bool>>       filter)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(TBase).IsAssignableFrom(entityType.ClrType)) continue;

            // In TPH hierarchies only the root may carry the filter.
            if (entityType.BaseType != null) continue;

            var parameter = Expression.Parameter(entityType.ClrType);

            var body = ReplacingExpressionVisitor.Replace(
                filter.Parameters.Single(),
                parameter,
                filter.Body);

            modelBuilder
                .Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
