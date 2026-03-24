using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    inn = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bank_account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    company_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inn = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phone_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    mail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_login_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    last_active_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_salt = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "auth",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    organization_id = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => x.id);
                    table.ForeignKey(
                        name: "FK_stations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "auth",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission = table.Column<int>(type: "integer", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    firmware_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    station_id = table.Column<long>(type: "bigint", nullable: false),
                    is_online = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_devices_stations_station_id",
                        column: x => x.station_id,
                        principalSchema: "app",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usage_sessions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    product_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    ended_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_usage_sessions_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "app",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usage_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clients_inn",
                schema: "app",
                table: "clients",
                column: "inn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_phone_number",
                schema: "app",
                table: "clients",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_serial_number",
                schema: "app",
                table: "devices",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_station_id",
                schema: "app",
                table: "devices",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_inn",
                schema: "auth",
                table: "organizations",
                column: "inn");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_name",
                schema: "auth",
                table: "organizations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_products_name",
                schema: "app",
                table: "products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_role_id_permission",
                schema: "auth",
                table: "role_permissions",
                columns: new[] { "role_id", "permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_organization_id_name",
                schema: "auth",
                table: "roles",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stations_organization_id_name",
                schema: "app",
                table: "stations",
                columns: new[] { "organization_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_device_id",
                schema: "app",
                table: "usage_sessions",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_user_id",
                schema: "app",
                table: "usage_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_mail",
                schema: "auth",
                table: "users",
                column: "mail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_phone_id",
                schema: "auth",
                table: "users",
                column: "phone_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_phone_number",
                schema: "auth",
                table: "users",
                column: "phone_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clients",
                schema: "app");

            migrationBuilder.DropTable(
                name: "products",
                schema: "app");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "usage_sessions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "app");

            migrationBuilder.DropTable(
                name: "users",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "stations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "auth");
        }
    }
}
