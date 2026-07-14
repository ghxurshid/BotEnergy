using Domain.Entities.BaseEntity;
using Domain.Exceptions;
using Domain.Helpers;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context
{
    public partial class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
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

                // Telefon raqam yozuvining yagona choke-point'i: insert yoki phone o'zgarishida
                // normalizatsiya qilib canonical (998XXXXXXXXX) formatni kafolatlaymiz. Bu — oxirgi
                // himoya; API validatsiya filtrlar odatda oldindan 400 bilan ushlaydi.
                if (entry.Entity is IHasPhoneNumber && entry.State is EntityState.Added or EntityState.Modified)
                    NormalizePhoneNumber(entry);
            }
        }

        private static void NormalizePhoneNumber(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            var phoneProp = entry.Property(nameof(IHasPhoneNumber.PhoneNumber));

            // Update paytida phone o'zgarmagan bo'lsa — tegmaymiz (mavjud legacy qiymatni buzmaslik uchun).
            if (entry.State == EntityState.Modified && !phoneProp.IsModified)
                return;

            var current = ((IHasPhoneNumber)entry.Entity).PhoneNumber;
            var normalized = PhoneNumberHelper.Normalize(current);

            if (!PhoneNumberHelper.IsValid(normalized))
                throw new InvalidPhoneNumberException(PhoneNumberHelper.ErrorMessage);

            if (!string.Equals(current, normalized, StringComparison.Ordinal))
                ((IHasPhoneNumber)entry.Entity).PhoneNumber = normalized!;
        }
    }
}
