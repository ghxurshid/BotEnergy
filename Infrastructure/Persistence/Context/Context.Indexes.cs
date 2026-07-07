using Domain.Entities;
using Domain.Entities.BaseEntity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

            modelBuilder.HasDefaultSchema(AppSchema);

            // PostGIS — StationEntity.Coordinates (geography Point) uchun.
            modelBuilder.HasPostgresExtension("postgis");

            ConfigurePlatformUser(modelBuilder);
            ConfigureCustomerUser(modelBuilder);
            ConfigureOrganization(modelBuilder);
            ConfigurePlatformRole(modelBuilder);
            ConfigureCustomerRole(modelBuilder);
            ConfigurePermission(modelBuilder);
            ConfigurePlatformRolePermission(modelBuilder);
            ConfigureCustomerRolePermission(modelBuilder);
            ConfigureStation(modelBuilder);
            ConfigureDevice(modelBuilder);
            ConfigureProduct(modelBuilder);
            ConfigureSession(modelBuilder);
            ConfigureProductProcess(modelBuilder);
            ConfigureMerchant(modelBuilder);
            ConfigurePaymentTransaction(modelBuilder);
            ConfigurePaymentTransactionStep(modelBuilder);

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

        /// <summary>Ikkala user jadvali uchun umumiy ustunlar (UserBase).</summary>
        private static void ConfigureUserCommon<T>(EntityTypeBuilder<T> b) where T : UserBase
        {
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

            b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
            b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

            b.Property(x => x.LastLoginDate).HasColumnName("last_login_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
            b.Property(x => x.LastActiveDate).HasColumnName("last_active_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);

            b.HasIndex(x => x.PhoneNumber).IsUnique();
            b.HasIndex(x => x.Mail).IsUnique();
            b.HasIndex(x => x.PhoneId);
        }

        private static void ConfigurePlatformUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlatformUserEntity>(b =>
            {
                b.ToTable("platform_users", AuthSchema);
                ConfigureUserCommon(b);

                b.Property(x => x.Type).HasColumnName("type").HasConversion<int>();

                b.Property(x => x.MerchantId).HasColumnName("merchant_id");
                b.HasOne(x => x.Merchant)
                    .WithMany(x => x.Users)
                    .HasForeignKey(x => x.MerchantId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.Role)
                    .WithMany()
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(x => x.MerchantId);
            });
        }

        private static void ConfigureCustomerUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomerUserEntity>(b =>
            {
                b.ToTable("customer_users", AuthSchema);
                ConfigureUserCommon(b);

                b.Property(x => x.Type).HasColumnName("type").HasConversion<int>();

                b.Property(x => x.Balance)
                    .HasColumnName("balance")
                    .HasColumnType("numeric(18,2)")
                    .HasDefaultValue(0m);

                b.Property(x => x.OrganizationId).HasColumnName("organization_id");
                b.HasOne(x => x.Organization)
                    .WithMany(x => x.CustomerUsers)
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(x => x.Role)
                    .WithMany()
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(x => x.OrganizationId);
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

        /// <summary>Ikkala rol jadvali uchun umumiy ustunlar (RoleBase).</summary>
        private static void ConfigureRoleCommon<T>(EntityTypeBuilder<T> b) where T : RoleBase
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(300);
            b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            b.Property(x => x.CreatedDate).HasColumnName("created_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
            b.Property(x => x.UpdatedDate).HasColumnName("updated_date").HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

            b.HasIndex(x => x.Name);
        }

        private static void ConfigurePlatformRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlatformRoleEntity>(b =>
            {
                b.ToTable("platform_roles", AuthSchema);
                ConfigureRoleCommon(b);

                b.Property(x => x.MerchantId).HasColumnName("merchant_id");
                b.HasOne(x => x.Merchant)
                    .WithMany(x => x.Roles)
                    .HasForeignKey(x => x.MerchantId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => x.MerchantId);
            });
        }

        private static void ConfigureCustomerRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomerRoleEntity>(b =>
            {
                b.ToTable("customer_roles", AuthSchema);
                ConfigureRoleCommon(b);

                b.Property(x => x.OrganizationId).HasColumnName("organization_id");
                b.HasOne(x => x.Organization)
                    .WithMany(x => x.Roles)
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => x.OrganizationId);
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

        private static void ConfigurePlatformRolePermission(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlatformRolePermissionEntity>(b =>
            {
                b.ToTable("platform_role_permissions", AuthSchema);

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
                    .WithMany(x => x.PlatformRolePermissions)
                    .HasForeignKey(x => x.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            });
        }

        private static void ConfigureCustomerRolePermission(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomerRolePermissionEntity>(b =>
            {
                b.ToTable("customer_role_permissions", AuthSchema);

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
                    .WithMany(x => x.CustomerRolePermissions)
                    .HasForeignKey(x => x.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
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
                b.Property(x => x.Address).HasColumnName("address").IsRequired().HasMaxLength(300);
                b.Property(x => x.Coordinates).HasColumnName("coordinates")
                    .HasColumnType("geography(Point,4326)").IsRequired();
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
                // Geografik so'rovlar (ST_DWithin/ST_Distance) uchun GiST spatial indeks.
                b.HasIndex(x => x.Coordinates).HasMethod("gist");
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
                b.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasColumnType(TimestampWithoutTimeZone);
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

        private static void ConfigureSession(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SessionEntity>(b =>
            {
                b.ToTable("sessions", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
                b.Property(x => x.DeviceId).HasColumnName("device_id");

                b.Property(x => x.SessionToken).HasColumnName("session_token").IsRequired().HasMaxLength(100);
                b.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
                b.Property(x => x.CloseReason).HasColumnName("close_reason").HasConversion<int?>();

                b.Property(x => x.CreatedAt).HasColumnName("created_at")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.ConnectedAt).HasColumnName("connected_at")
                    .HasColumnType(TimestampWithoutTimeZone);
                b.Property(x => x.ClosedAt).HasColumnName("closed_at")
                    .HasColumnType(TimestampWithoutTimeZone);
                b.Property(x => x.LastActivityAt).HasColumnName("last_activity_at")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);

                b.Property(x => x.CreatedDate).HasColumnName("created_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.User)
                    .WithMany(x => x.Sessions)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                // FK → auth.customer_users (sessiya faqat Customer uchun)

                b.HasOne(x => x.Device)
                    .WithMany(x => x.Sessions)
                    .HasForeignKey(x => x.DeviceId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(x => x.Processes)
                    .WithOne(x => x.Session!)
                    .HasForeignKey(x => x.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => x.SessionToken).IsUnique();
                b.HasIndex(x => new { x.UserId, x.Status });
                b.HasIndex(x => x.LastActivityAt);
            });
        }

        private static void ConfigureProductProcess(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductProcessEntity>(b =>
            {
                b.ToTable("product_processes", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.SessionId).HasColumnName("session_id").IsRequired();
                b.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();

                b.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(200);
                b.Property(x => x.PricePerUnit).HasColumnName("price_per_unit")
                    .HasColumnType("numeric(18,2)").HasDefaultValue(0m);
                b.Property(x => x.Unit).HasColumnName("unit").HasConversion<int>();

                b.Property(x => x.RequestedAmount).HasColumnName("requested_amount")
                    .HasColumnType("numeric(18,4)").HasDefaultValue(0m);
                b.Property(x => x.GivenAmount).HasColumnName("given_amount")
                    .HasColumnType("numeric(18,4)").HasDefaultValue(0m);

                b.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
                b.Property(x => x.EndReason).HasColumnName("end_reason").HasConversion<int?>();

                b.Property(x => x.IsBalanceDeducted).HasColumnName("is_balance_deducted").HasDefaultValue(false);
                b.Property(x => x.LastTelemetrySequence).HasColumnName("last_telemetry_sequence").HasDefaultValue(0L);

                b.Property(x => x.RowVersion).HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();

                b.Property(x => x.StartedAt).HasColumnName("started_at")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.PausedAt).HasColumnName("paused_at")
                    .HasColumnType(TimestampWithoutTimeZone);
                b.Property(x => x.EndedAt).HasColumnName("ended_at")
                    .HasColumnType(TimestampWithoutTimeZone);
                b.Property(x => x.CreatedDate).HasColumnName("created_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.Product)
                    .WithMany(x => x.Processes)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(x => x.SessionId);
                b.HasIndex(x => new { x.SessionId, x.Status });
                b.HasIndex(x => x.StartedAt);
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

        private static void ConfigurePaymentTransaction(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PaymentTransactionEntity>(b =>
            {
                b.ToTable("payment_transactions", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.PayeeType).HasColumnName("payee_type").HasConversion<int>().IsRequired();
                b.Property(x => x.UserId).HasColumnName("user_id");
                b.Property(x => x.OrganizationId).HasColumnName("organization_id");
                b.Property(x => x.InitiatedByUserId).HasColumnName("initiated_by_user_id").IsRequired();

                b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
                b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();

                b.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                b.Property(x => x.Provider).HasColumnName("provider").HasConversion<int>().IsRequired();

                b.Property(x => x.ProviderReceiptId).HasColumnName("provider_receipt_id").HasMaxLength(64);
                b.Property(x => x.ProviderOrderId).HasColumnName("provider_order_id").HasMaxLength(64).IsRequired();
                b.Property(x => x.ProviderState).HasColumnName("provider_state");

                b.Property(x => x.DeviceSerial).HasColumnName("device_serial").HasMaxLength(100);
                b.Property(x => x.SessionId).HasColumnName("session_id");
                b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128);
                b.Property(x => x.FailureReason).HasColumnName("failure_reason");

                b.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType(TimestampWithoutTimeZone);

                b.Property(x => x.CreatedDate).HasColumnName("created_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.Organization)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(x => x.Session)
                    .WithMany()
                    .HasForeignKey(x => x.SessionId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasMany(x => x.Steps)
                    .WithOne(x => x.PaymentTransaction!)
                    .HasForeignKey(x => x.PaymentTransactionId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => x.ProviderOrderId).IsUnique();
                b.HasIndex(x => x.ProviderReceiptId);
                b.HasIndex(x => new { x.UserId, x.Status, x.CreatedDate });
                b.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedDate });
                b.HasIndex(x => new { x.InitiatedByUserId, x.CreatedDate });
                b.HasIndex(x => new { x.Status, x.CreatedDate });
            });
        }

        private static void ConfigurePaymentTransactionStep(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PaymentTransactionStepEntity>(b =>
            {
                b.ToTable("payment_transaction_steps", AppSchema);

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                b.Property(x => x.PaymentTransactionId).HasColumnName("payment_transaction_id").IsRequired();
                b.Property(x => x.StepType).HasColumnName("step_type").HasConversion<int>().IsRequired();
                b.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

                b.Property(x => x.RequestPayload).HasColumnName("request_payload").HasColumnType("jsonb");
                b.Property(x => x.ResponsePayload).HasColumnName("response_payload").HasColumnType("jsonb");
                b.Property(x => x.Message).HasColumnName("message");

                b.Property(x => x.OccurredAt).HasColumnName("occurred_at")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);

                b.Property(x => x.CreatedDate).HasColumnName("created_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.UpdatedDate).HasColumnName("updated_date")
                    .HasColumnType(TimestampWithoutTimeZone).HasDefaultValueSql(LocalTimestampDefaultSql);
                b.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

                b.HasIndex(x => new { x.PaymentTransactionId, x.OccurredAt });
            });
        }
    }
}
