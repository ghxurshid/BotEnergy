using Domain.Entities;
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

                if (entry.Entity is UserBase && entry.State is EntityState.Added or EntityState.Modified)
                    NormalizeMail(entry);
            }
        }

        /// <summary>
        /// Pochta manzilining yagona choke-point'i: bo'shliqlar kesiladi va kichik harfga
        /// o'tkaziladi.
        ///
        /// Nega kerak: <c>mail</c> ustunida unique indeks bor, PostgreSQL esa uni registrga
        /// SEZGIR solishtiradi — ya'ni "Ali@mail.uz" va "ali@mail.uz" baza uchun ikki xil
        /// qiymat, foydalanuvchi uchun esa bitta. Bu yerda normallashtirilgach indeks amalda
        /// registrga befarq bo'lib qoladi va dublikat INSERT paytida emas, oldindan
        /// tekshiruvda ushlanadi.
        ///
        /// Mavjud aralash registrli qatorlar ham har qanday keyingi saqlashda jimgina
        /// to'g'rilanadi — alohida migratsiya kerak emas.
        /// </summary>
        private static void NormalizeMail(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            var mailProp = entry.Property(nameof(UserBase.Mail));

            // Update paytida pochta o'zgarmagan bo'lsa tegmaymiz — keraksiz UPDATE qilmaslik uchun.
            if (entry.State == EntityState.Modified && !mailProp.IsModified)
                return;

            var current = ((UserBase)entry.Entity).Mail;
            if (string.IsNullOrWhiteSpace(current))
                return;

            var normalized = current.Trim().ToLowerInvariant();
            if (!string.Equals(current, normalized, StringComparison.Ordinal))
                ((UserBase)entry.Entity).Mail = normalized;
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
