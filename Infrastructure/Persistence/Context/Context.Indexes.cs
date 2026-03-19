using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context
{
    public partial class Context
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureUser(modelBuilder);
            ConfigureProduct(modelBuilder);
            ConfigureUsageSession(modelBuilder);
            ConfigureClient(modelBuilder);
            ConfigureEmptyEntities(modelBuilder);

            OnModelCreatingPartial(modelBuilder);
        }

        private static void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("users");

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedOnAdd();

                b.Property(x => x.PhoneId).IsRequired().HasMaxLength(128);
                b.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(32);
                b.Property(x => x.Mail).IsRequired().HasMaxLength(256);

                b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(256);
                b.Property(x => x.PasswordSalt).IsRequired().HasMaxLength(256);

                b.Property(x => x.Balance).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
                b.Property(x => x.IsBlocked).HasDefaultValue(false);
                b.Property(x => x.IsVerified).HasDefaultValue(false);

                b.Property(x => x.CreatedDate).HasDefaultValueSql("now()");
                b.Property(x => x.UpdatedDate).HasDefaultValueSql("now()");

                b.HasIndex(x => x.PhoneNumber).IsUnique();
                b.HasIndex(x => x.Mail).IsUnique();
                b.HasIndex(x => x.PhoneId);
            });
        }

        private static void ConfigureProduct(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductEntity>(b =>
            {
                b.ToTable("products");

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedNever();

                b.Property(x => x.Name).IsRequired().HasMaxLength(200);
                b.HasIndex(x => x.Name);

                b.Property(x => x.Type).HasConversion<int>();
                b.Property(x => x.Unit).HasConversion<int>();

                b.Property(x => x.Price).HasColumnType("numeric(18,2)");
                b.Property(x => x.IsActive).HasDefaultValue(true);

                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });
        }

        private static void ConfigureUsageSession(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UsageSessionEntity>(b =>
            {
                b.ToTable("usage_sessions");

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedNever();

                b.Property(x => x.UserId).IsRequired();
                b.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
                b.Property(x => x.ProductType).IsRequired().HasMaxLength(64);

                b.Property(x => x.Quantity).HasColumnType("numeric(18,4)");
                b.Property(x => x.Price).HasColumnType("numeric(18,2)");

                b.Property(x => x.StartedAt).HasDefaultValueSql("now()");

                b.HasIndex(x => x.DeviceId);
                b.HasIndex(x => x.UserId);
            });
        }

        private static void ConfigureClient(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientEntity>(b =>
            {
                b.ToTable("clients");

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedNever();

                b.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(32);
                b.Property(x => x.Inn).IsRequired().HasMaxLength(32);
                b.Property(x => x.BankAccount).IsRequired().HasMaxLength(64);
                b.Property(x => x.CompanyName).IsRequired().HasMaxLength(256);

                b.Property(x => x.IsActive).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

                b.HasIndex(x => x.PhoneNumber).IsUnique();
                b.HasIndex(x => x.Inn).IsUnique();
            });
        }

        private static void ConfigureEmptyEntities(ModelBuilder modelBuilder)
        {
            // Bu entity’lar hozircha property/key’lari yo‘q. EF Core runtime’da xato chiqarmasligi uchun
            // vaqtincha keyless qilib qo‘yamiz. Keyin domain entity’lar to‘ldirilganda normal mapping qilinadi.
            modelBuilder.Entity<OrganizationEntity>().ToTable("organizations").HasNoKey();
            modelBuilder.Entity<RoleEntity>().ToTable("roles").HasNoKey();
            modelBuilder.Entity<RolePermissionEntity>().ToTable("role_permissions").HasNoKey();
            modelBuilder.Entity<StationEntity>().ToTable("stations").HasNoKey();
            modelBuilder.Entity<DeviceEntity>().ToTable("devices").HasNoKey();
        }
    }
}
