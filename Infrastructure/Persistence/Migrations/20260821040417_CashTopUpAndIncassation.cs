using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashTopUpAndIncassation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_platform_users_mail",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropIndex(
                name: "IX_platform_users_phone_number",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropIndex(
                name: "IX_platform_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "platform_role_permissions");

            migrationBuilder.DropIndex(
                name: "IX_permissions_name",
                schema: "auth",
                table: "permissions");

            migrationBuilder.DropIndex(
                name: "IX_merchants_inn",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_merchants_phone_number",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_devices_serial_number",
                schema: "app",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_customer_users_mail",
                schema: "auth",
                table: "customer_users");

            migrationBuilder.DropIndex(
                name: "IX_customer_users_phone_number",
                schema: "auth",
                table: "customer_users");

            migrationBuilder.DropIndex(
                name: "IX_customer_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "customer_role_permissions");

            migrationBuilder.AddColumn<decimal>(
                name: "cash_balance",
                schema: "app",
                table: "devices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "cash_last_collected_at",
                schema: "app",
                table: "devices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_collections",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    merchant_id = table.Column<long>(type: "bigint", nullable: false),
                    station_id = table.Column<long>(type: "bigint", nullable: false),
                    incassator_user_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    counted_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "UZS"),
                    requested_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    box_opened_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_collections", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_collections_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "app",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_collections_platform_users_incassator_user_id",
                        column: x => x.incassator_user_id,
                        principalSchema: "auth",
                        principalTable: "platform_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_sessions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    card_masked = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    card_token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    accepted_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    bill_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "UZS"),
                    payout_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_sessions_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "app",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_session_bills",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cash_session_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    denomination = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "UZS"),
                    bill_seq = table.Column<int>(type: "integer", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_session_bills", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_session_bills_cash_sessions_cash_session_id",
                        column: x => x.cash_session_id,
                        principalSchema: "app",
                        principalTable: "cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_mail",
                schema: "auth",
                table: "platform_users",
                column: "mail",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_phone_number",
                schema: "auth",
                table: "platform_users",
                column: "phone_number",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_platform_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "platform_role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_name",
                schema: "auth",
                table: "permissions",
                column: "name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_inn",
                schema: "app",
                table: "merchants",
                column: "inn",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_phone_number",
                schema: "app",
                table: "merchants",
                column: "phone_number",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_devices_serial_number",
                schema: "app",
                table: "devices",
                column: "serial_number",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_mail",
                schema: "auth",
                table: "customer_users",
                column: "mail",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_phone_number",
                schema: "auth",
                table: "customer_users",
                column: "phone_number",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_customer_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "customer_role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_cash_collections_device_open",
                schema: "app",
                table: "cash_collections",
                column: "device_id",
                unique: true,
                filter: "status IN (0, 1) AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_cash_collections_incassator_user_id_requested_at",
                schema: "app",
                table: "cash_collections",
                columns: new[] { "incassator_user_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_collections_merchant_id_requested_at",
                schema: "app",
                table: "cash_collections",
                columns: new[] { "merchant_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_session_bills_cash_session_id_bill_seq",
                schema: "app",
                table: "cash_session_bills",
                columns: new[] { "cash_session_id", "bill_seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_session_bills_device_id_accepted_at",
                schema: "app",
                table: "cash_session_bills",
                columns: new[] { "device_id", "accepted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_device_active",
                schema: "app",
                table: "cash_sessions",
                column: "device_id",
                unique: true,
                filter: "status IN (0, 1) AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_idempotency_key",
                schema: "app",
                table: "cash_sessions",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_serial_number_status",
                schema: "app",
                table: "cash_sessions",
                columns: new[] { "serial_number", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_status_next_attempt_at",
                schema: "app",
                table: "cash_sessions",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_collections",
                schema: "app");

            migrationBuilder.DropTable(
                name: "cash_session_bills",
                schema: "app");

            migrationBuilder.DropTable(
                name: "cash_sessions",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_platform_users_mail",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropIndex(
                name: "IX_platform_users_phone_number",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropIndex(
                name: "IX_platform_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "platform_role_permissions");

            migrationBuilder.DropIndex(
                name: "IX_permissions_name",
                schema: "auth",
                table: "permissions");

            migrationBuilder.DropIndex(
                name: "IX_merchants_inn",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_merchants_phone_number",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropIndex(
                name: "IX_devices_serial_number",
                schema: "app",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_customer_users_mail",
                schema: "auth",
                table: "customer_users");

            migrationBuilder.DropIndex(
                name: "IX_customer_users_phone_number",
                schema: "auth",
                table: "customer_users");

            migrationBuilder.DropIndex(
                name: "IX_customer_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "customer_role_permissions");

            migrationBuilder.DropColumn(
                name: "cash_balance",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "cash_last_collected_at",
                schema: "app",
                table: "devices");

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_mail",
                schema: "auth",
                table: "platform_users",
                column: "mail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_phone_number",
                schema: "auth",
                table: "platform_users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "platform_role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_name",
                schema: "auth",
                table: "permissions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchants_inn",
                schema: "app",
                table: "merchants",
                column: "inn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchants_phone_number",
                schema: "app",
                table: "merchants",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_serial_number",
                schema: "app",
                table: "devices",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_mail",
                schema: "auth",
                table: "customer_users",
                column: "mail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_phone_number",
                schema: "auth",
                table: "customer_users",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_role_permissions_role_id_permission_id",
                schema: "auth",
                table: "customer_role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);
        }
    }
}
