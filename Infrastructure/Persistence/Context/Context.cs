using Domain.Entities;
using Domain.Entities.BaseEntity;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context
{
    public partial class Context : DbContext
    {
        public Context(DbContextOptions<Context> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyEntityTrackingLogic();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            ApplyEntityTrackingLogic();
            return base.SaveChanges();

        }

        private void ApplyEntityTrackingLogic()
        {
            var entries = ChangeTracker.Entries<Entity>();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedDate = DateTime.Now;
                }
            }
        }
    }
}

