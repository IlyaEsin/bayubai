using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Bayubai.SharedKernel.Consultants;

public static class ConsultantQueryFilters
{
    // Referencing the context instance makes EF read CurrentConsultantId per query instead of baking it into the cached model.
    public static ModelBuilder ApplyConsultantQueryFilters<TContext>(this ModelBuilder modelBuilder, TContext context)
        where TContext : DbContext, IConsultantScopedDbContext
    {
        var currentConsultantId = Expression.Property(
            Expression.Constant(context),
            nameof(IConsultantScopedDbContext.CurrentConsultantId));

        var ownedTypes = modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => typeof(IConsultantOwned).IsAssignableFrom(type))
            .ToList();

        foreach (var type in ownedTypes)
        {
            var entity = Expression.Parameter(type, "entity");
            var ownerId = Expression.Convert(
                Expression.Property(entity, nameof(IConsultantOwned.ConsultantId)),
                typeof(Guid?));
            modelBuilder.Entity(type).HasQueryFilter(Expression.Lambda(Expression.Equal(ownerId, currentConsultantId), entity));
        }

        return modelBuilder;
    }
}
