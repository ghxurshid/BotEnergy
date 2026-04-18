using Domain.Entities;
using Domain.Entities.BaseEntity;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Context
{
    public partial class AppDbContext
    {
        private const string AppSchema = "app";
        private const string AuthSchema = "auth";
        private const string TimestampWithoutTimeZone = "timestamp without time zone";
        private const string LocalTimestampDefaultSql = "LOCALTIMESTAMP";

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresEnum<UserType>("auth", "user_type");

            modelBuilder.HasDefaultSchema(AppSchema);

            ConfigureUser(modelBuilder);
            ConfigureOrganization(modelBuilder);
            ConfigureRole(modelBuilder);
            ConfigurePermission(modelBuilder);
            ConfigureRolePermission(modelBuilder);
            ConfigureUserRole(modelBuilder);
            ConfigureStation(modelBuilder);
            ConfigureDevice(modelBuilder);
            ConfigureProduct(modelBuilder);
            ConfigureUsageSession(modelBuilder);
            ConfigureMerchant(modelBuilder);

            ApplyGlobalSoftDeleteFilter(modelBuilder);

            OnModelCreatingPartial(modelBuilder);
        }

        private static void ApplyGlobalSoftDeleteFilter(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
                    continue;

                if (entityType.BaseType is not null)
                    continue;

                var method = typeof(AppDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, new object[] { modelBuilder });
            }
        }

        private static void SetSoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : Entity
        {
            modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
        }

        private static void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("users", AuthSchema);

                b.HasDiscriminator<UserType>("user_type")
                    .HasValue<NaturalUserEntity>(UserType.NaturalPerson)
                    .HasValue<LegalUserEntity>(UserType.LegalEntity)
                    .HasValue<MerchantUserEntity>(UserType.MerchantPerson);

                b.Property<UserType>("user_type")
                    .HasColumnType("auth.user_type")
                    .HasColumnName("user_type");

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.PhoneId).HasColumnName("phone_id").IsRequired().HasMaxLength(128);
                b.Property(x => x.PhoneNumber).HasColumnName("phone_number").IsRequired().HasMaxLength(32);
                b.Property(x => x.Mail).HasColumnName("mail").IsRequired().HasMaxLength(256);

                b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
                b.Property(x => x.PasswordSalt).HasColumnName("password_salt").HasMaxLength(256);

                b.Property(x => x.IsBlocked).HasColumnName("is_blocked").HasDefaultValue(false);
                b.Property(x => x.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);
                b.Property(x => x.IsOtpVerified).HasColumnName("is_otp_verified").HasDefaultValue(false);

                b.Property(x => x.RoleId).HasColumnName("role_id");
                b.HasOne(x => x.Role)
                    .WithMany()
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.Property(x => x.LastLoginDate).HasColumnName("last_login_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.LastActiveDate).HasColumnName("last_active_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);

                b.HasIndex(x => x.PhoneNumber).IsUnique();
                b.HasIndex(x => x.Mail).IsUnique();
                b.HasIndex(x => x.PhoneId);
            });

            modelBuilder.Entity<NaturalUserEntity>(b =>
            {
                b.Property(x => x.Balance)
                    .HasColumnName("balance")
                    .HasColumnType("numeric(18,2)")
                    .HasDefaultValue(0m);
            });

            modelBuilder.Entity<LegalUserEntity>(b =>
            {
                b.Property(x => x.OrganizationId).HasColumnName("organization_id");
                b.HasOne(x => x.Organization)
                    .WithMany(x => x.LegalUsers)
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<MerchantUserEntity>(b =>
            {
                b.Property(x => x.StationId).HasColumnName("station_id");
                b.HasOne(x => x.Station)
                    .WithMany()
                    .HasForeignKey(x => x.StationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureOrganization(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrganizationEntity>(b =>
            {
                b.ToTable("organizations", AuthSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
                b.Property(x => x.Inn).HasColumnName("inn").HasMaxLength(32);
                b.Property(x => x.Address).HasColumnName("address").HasMaxLength(300);
                b.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);

                b.Property(x => x.Balance).HasColumnName("balance").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
                b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasIndex(x => x.Name);
                b.HasIndex(x => x.Inn);
            });
        }

        private static void ConfigureRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RoleEntity>(b =>
            {
                b.ToTable("roles", AuthSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                b.Property(x => x.Description).HasColumnName("description").HasMaxLength(300);
                b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasIndex(x => x.Name);
            });
        }

        private static void ConfigurePermission(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PermissionEntity>(b =>
            {
                b.ToTable("permissions", AuthSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                b.Property(x => x.Description).HasColumnName("description").HasMaxLength(300);
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasIndex(x => x.Name).IsUnique();
            });
        }

        private static void ConfigureRolePermission(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RolePermissionEntity>(b =>
            {
                b.ToTable("role_permissions", AuthSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
                b.Property(x => x.PermissionId).HasColumnName("permission_id").IsRequired();
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.Role)
                    .WithMany(x => x.RolePermissions)
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Permission)
                    .WithMany(x => x.RolePermissions)
                    .HasForeignKey(x => x.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            });
        }

        private static void ConfigureUserRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserRoleEntity>(b =>
            {
                b.ToTable("user_roles", AuthSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
                b.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.User)
                    .WithMany(x => x.UserRoles)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Role)
                    .WithMany(x => x.UserRoles)
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
            });
        }

        private static void ConfigureStation(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StationEntity>(b =>
            {
                b.ToTable("stations", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(150);
                b.Property(x => x.Location).HasColumnName("location").HasMaxLength(300);
                b.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
                b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.Merchant)
                    .WithMany(x => x.Stations)
                    .HasForeignKey(x => x.MerchantId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => new { x.MerchantId, x.Name });
            });
        }

        private static void ConfigureDevice(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeviceEntity>(b =>
            {
                b.ToTable("devices", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.SerialNumber).HasColumnName("serial_number").IsRequired().HasMaxLength(100);
                b.Property(x => x.SecretKey).HasColumnName("secret_key").IsRequired().HasMaxLength(64);
                b.Property(x => x.DeviceType).HasColumnName("device_type").HasConversion<int>();
                b.Property(x => x.Model).HasColumnName("model").HasMaxLength(100);
                b.Property(x => x.FirmwareVersion).HasColumnName("firmware_version").HasMaxLength(50);
                b.Property(x => x.StationId).HasColumnName("station_id").IsRequired();
                b.Property(x => x.IsOnline).HasColumnName("is_online").HasDefaultValue(false);
                b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.Station)
                    .WithMany(x => x.Devices)
                    .HasForeignKey(x => x.StationId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => x.SerialNumber).IsUnique();
                b.HasIndex(x => x.StationId);
            });
        }

        private static void ConfigureProduct(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductEntity>(b =>
            {
                b.ToTable("products", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
                b.HasIndex(x => x.Name);

                b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);

                b.Property(x => x.Type).HasColumnName("type").HasConversion<int>();
                b.Property(x => x.Unit).HasColumnName("unit").HasConversion<int>();

                b.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(18,2)");
                b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

                b.Property(x => x.DeviceId).HasColumnName("device_id").IsRequired();
                b.HasOne(x => x.Device)
                    .WithMany(x => x.Products)
                    .HasForeignKey(x => x.DeviceId)
                    .OnDelete(DeleteBehavior.NoAction);

                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasIndex(x => x.DeviceId);
            });
        }

        private static void ConfigureUsageSession(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UsageSessionEntity>(b =>
            {
                b.ToTable("usage_sessions", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
                b.Property(x => x.DeviceId).HasColumnName("device_id");

                b.Property(x => x.SessionToken).HasColumnName("session_token").IsRequired().HasMaxLength(100);
                b.Property(x => x.Status).HasColumnName("status")
                    .HasConversion<string>().HasMaxLength(30);

                b.Property(x => x.ProductId).HasColumnName("product_id");
                b.Property(x => x.Unit).HasColumnName("unit").HasConversion<int?>();
                b.Property(x => x.RequestedQuantity).HasColumnName("requested_quantity")
                    .HasColumnType("numeric(18,4)");
                b.Property(x => x.DeliveredQuantity).HasColumnName("delivered_quantity")
                    .HasColumnType("numeric(18,4)").HasDefaultValue(0m);
                b.Property(x => x.PricePerUnit).HasColumnName("price_per_unit")
                    .HasColumnType("numeric(18,2)").HasDefaultValue(0m);
                b.Property(x => x.EndReason).HasColumnName("end_reason").HasMaxLength(50);

                // Snapshot maydonlar
                b.Property(x => x.UserPhoneNumber).HasColumnName("user_phone_number").HasMaxLength(20);
                b.Property(x => x.DeviceSerialNumber).HasColumnName("device_serial_number").HasMaxLength(100);
                b.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(200);

                b.Property(x => x.StartedAt).HasColumnName("started_at")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.DeviceConnectedAt).HasColumnName("device_connected_at")
                    .HasColumnType(TimestampWithoutTimeZone);
                b.Property(x => x.LastActivityAt).HasColumnName("last_activity_at")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.EndedAt).HasColumnName("ended_at")
                    .HasColumnType(TimestampWithoutTimeZone);
                b.Property(x => x.CreatedDate).HasColumnName("created_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.User)
                    .WithMany(x => x.UsageSessions)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.Device)
                    .WithMany(x => x.UsageSessions)
                    .HasForeignKey(x => x.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.Product)
                    .WithMany(x => x.UsageSessions)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => x.SessionToken).IsUnique();
                b.HasIndex(x => new { x.UserId, x.Status });
                b.HasIndex(x => x.LastActivityAt);
            });
        }

        private static void ConfigureMerchant(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MerchantEntity>(b =>
            {
                b.ToTable("merchants", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.PhoneNumber).HasColumnName("phone_number").IsRequired().HasMaxLength(32);
                b.Property(x => x.Inn).HasColumnName("inn").IsRequired().HasMaxLength(32);
                b.Property(x => x.BankAccount).HasColumnName("bank_account").IsRequired().HasMaxLength(64);
                b.Property(x => x.CompanyName).HasColumnName("company_name").IsRequired().HasMaxLength(256);

                b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasIndex(x => x.PhoneNumber).IsUnique();
                b.HasIndex(x => x.Inn).IsUnique();
            });
        }
    }
}
