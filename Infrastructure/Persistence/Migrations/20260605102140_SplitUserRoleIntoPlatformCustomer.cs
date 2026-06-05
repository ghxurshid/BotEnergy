using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitUserRoleIntoPlatformCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_transactions_users_user_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_roles_merchants_merchant_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "FK_roles_organizations_organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "FK_sessions_users_user_id",
                schema: "app",
                table: "sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_users_organizations_organization_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_role_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_stations_station_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "auth");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_organization_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_roles",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "balance",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_type",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "role_type",
                schema: "auth",
                table: "roles");

            migrationBuilder.RenameTable(
                name: "users",
                schema: "auth",
                newName: "platform_users",
                newSchema: "auth");

            migrationBuilder.RenameTable(
                name: "roles",
                schema: "auth",
                newName: "platform_roles",
                newSchema: "auth");

            migrationBuilder.RenameColumn(
                name: "station_id",
                schema: "auth",
                table: "platform_users",
                newName: "merchant_id");

            migrationBuilder.RenameIndex(
                name: "IX_users_station_id",
                schema: "auth",
                table: "platform_users",
                newName: "IX_platform_users_merchant_id");

            migrationBuilder.RenameIndex(
                name: "IX_users_role_id",
                schema: "auth",
                table: "platform_users",
                newName: "IX_platform_users_role_id");

            migrationBuilder.RenameIndex(
                name: "IX_users_phone_number",
                schema: "auth",
                table: "platform_users",
                newName: "IX_platform_users_phone_number");

            migrationBuilder.RenameIndex(
                name: "IX_users_phone_id",
                schema: "auth",
                table: "platform_users",
                newName: "IX_platform_users_phone_id");

            migrationBuilder.RenameIndex(
                name: "IX_users_mail",
                schema: "auth",
                table: "platform_users",
                newName: "IX_platform_users_mail");

            migrationBuilder.RenameIndex(
                name: "IX_roles_name",
                schema: "auth",
                table: "platform_roles",
                newName: "IX_platform_roles_name");

            migrationBuilder.RenameIndex(
                name: "IX_roles_merchant_id",
                schema: "auth",
                table: "platform_roles",
                newName: "IX_platform_roles_merchant_id");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role,platform_role")
                .OldAnnotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person,platform");

            migrationBuilder.AddColumn<int>(
                name: "type",
                schema: "auth",
                table: "platform_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_platform_users",
                schema: "auth",
                table: "platform_users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_platform_roles",
                schema: "auth",
                table: "platform_roles",
                column: "id");

            migrationBuilder.CreateTable(
                name: "customer_roles",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<long>(type: "bigint", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_roles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "auth",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_role_permissions",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_platform_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "auth",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_platform_role_permissions_platform_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "platform_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_role_permissions",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_role_permissions_customer_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "customer_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "auth",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_users",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<int>(type: "integer", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    organization_id = table.Column<long>(type: "bigint", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    phone_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    mail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_otp_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    role_id = table.Column<long>(type: "bigint", nullable: true),
                    last_login_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    last_active_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    password_salt = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_users_customer_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "customer_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_customer_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "auth",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_role_permissions_permission_id",
                schema: "auth",
                table: "customer_role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "customer_role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_roles_name",
                schema: "auth",
                table: "customer_roles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_customer_roles_organization_id",
                schema: "auth",
                table: "customer_roles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_mail",
                schema: "auth",
                table: "customer_users",
                column: "mail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_organization_id",
                schema: "auth",
                table: "customer_users",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_phone_id",
                schema: "auth",
                table: "customer_users",
                column: "phone_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_phone_number",
                schema: "auth",
                table: "customer_users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_role_id",
                schema: "auth",
                table: "customer_users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_platform_role_permissions_permission_id",
                schema: "auth",
                table: "platform_role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_platform_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "platform_role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_transactions_customer_users_user_id",
                schema: "app",
                table: "payment_transactions",
                column: "user_id",
                principalSchema: "auth",
                principalTable: "customer_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_platform_roles_merchants_merchant_id",
                schema: "auth",
                table: "platform_roles",
                column: "merchant_id",
                principalSchema: "app",
                principalTable: "merchants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_platform_users_merchants_merchant_id",
                schema: "auth",
                table: "platform_users",
                column: "merchant_id",
                principalSchema: "app",
                principalTable: "merchants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_platform_users_platform_roles_role_id",
                schema: "auth",
                table: "platform_users",
                column: "role_id",
                principalSchema: "auth",
                principalTable: "platform_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_sessions_customer_users_user_id",
                schema: "app",
                table: "sessions",
                column: "user_id",
                principalSchema: "auth",
                principalTable: "customer_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_transactions_customer_users_user_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_platform_roles_merchants_merchant_id",
                schema: "auth",
                table: "platform_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_platform_users_merchants_merchant_id",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropForeignKey(
                name: "FK_platform_users_platform_roles_role_id",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropForeignKey(
                name: "FK_sessions_customer_users_user_id",
                schema: "app",
                table: "sessions");

            migrationBuilder.DropTable(
                name: "customer_role_permissions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "customer_users",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "platform_role_permissions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "customer_roles",
                schema: "auth");

            migrationBuilder.DropPrimaryKey(
                name: "PK_platform_users",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_platform_roles",
                schema: "auth",
                table: "platform_roles");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.RenameTable(
                name: "platform_users",
                schema: "auth",
                newName: "users",
                newSchema: "auth");

            migrationBuilder.RenameTable(
                name: "platform_roles",
                schema: "auth",
                newName: "roles",
                newSchema: "auth");

            migrationBuilder.RenameColumn(
                name: "merchant_id",
                schema: "auth",
                table: "users",
                newName: "station_id");

            migrationBuilder.RenameIndex(
                name: "IX_platform_users_role_id",
                schema: "auth",
                table: "users",
                newName: "IX_users_role_id");

            migrationBuilder.RenameIndex(
                name: "IX_platform_users_phone_number",
                schema: "auth",
                table: "users",
                newName: "IX_users_phone_number");

            migrationBuilder.RenameIndex(
                name: "IX_platform_users_phone_id",
                schema: "auth",
                table: "users",
                newName: "IX_users_phone_id");

            migrationBuilder.RenameIndex(
                name: "IX_platform_users_merchant_id",
                schema: "auth",
                table: "users",
                newName: "IX_users_station_id");

            migrationBuilder.RenameIndex(
                name: "IX_platform_users_mail",
                schema: "auth",
                table: "users",
                newName: "IX_users_mail");

            migrationBuilder.RenameIndex(
                name: "IX_platform_roles_name",
                schema: "auth",
                table: "roles",
                newName: "IX_roles_name");

            migrationBuilder.RenameIndex(
                name: "IX_platform_roles_merchant_id",
                schema: "auth",
                table: "roles",
                newName: "IX_roles_merchant_id");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role,platform_role")
                .Annotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person,platform");

            migrationBuilder.AddColumn<decimal>(
                name: "balance",
                schema: "auth",
                table: "users",
                type: "numeric(18,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "organization_id",
                schema: "auth",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "user_type",
                schema: "auth",
                table: "users",
                type: "auth.user_type",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "organization_id",
                schema: "auth",
                table: "roles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "role_type",
                schema: "auth",
                table: "roles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                schema: "auth",
                table: "users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_roles",
                schema: "auth",
                table: "roles",
                column: "id");

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    permission_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "auth",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_organization_id",
                schema: "auth",
                table: "users",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_organization_id",
                schema: "auth",
                table: "roles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                schema: "auth",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "auth",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user_id_role_id",
                schema: "auth",
                table: "user_roles",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_transactions_users_user_id",
                schema: "app",
                table: "payment_transactions",
                column: "user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_roles_merchants_merchant_id",
                schema: "auth",
                table: "roles",
                column: "merchant_id",
                principalSchema: "app",
                principalTable: "merchants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_roles_organizations_organization_id",
                schema: "auth",
                table: "roles",
                column: "organization_id",
                principalSchema: "auth",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sessions_users_user_id",
                schema: "app",
                table: "sessions",
                column: "user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_organizations_organization_id",
                schema: "auth",
                table: "users",
                column: "organization_id",
                principalSchema: "auth",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_role_id",
                schema: "auth",
                table: "users",
                column: "role_id",
                principalSchema: "auth",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_stations_station_id",
                schema: "auth",
                table: "users",
                column: "station_id",
                principalSchema: "app",
                principalTable: "stations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
