using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldInvoiceFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "funding_source",
                schema: "app",
                table: "product_processes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "payment_session_id",
                schema: "app",
                table: "product_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payme_cashbox_id",
                schema: "app",
                table: "merchants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "payme_enabled",
                schema: "app",
                table: "merchants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "payme_key",
                schema: "app",
                table: "merchants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_sessions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    merchant_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    hold_balance_tiyin = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    consumed_tiyin = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_sessions_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "app",
                        principalTable: "merchants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_sessions_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "app",
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hold_invoices",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payment_session_id = table.Column<long>(type: "bigint", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    amount_tiyin = table.Column<long>(type: "bigint", nullable: false),
                    consumed_tiyin = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    capture_amount_tiyin = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    provider_receipt_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    provider_order_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_state = table.Column<int>(type: "integer", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    hold_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    settled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hold_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_hold_invoices_payment_sessions_payment_session_id",
                        column: x => x.payment_session_id,
                        principalSchema: "app",
                        principalTable: "payment_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hold_invoice_steps",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    hold_invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    payment_session_id = table.Column<long>(type: "bigint", nullable: false),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    merchant_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    step_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    request_payload = table.Column<string>(type: "jsonb", nullable: true),
                    response_payload = table.Column<string>(type: "jsonb", nullable: true),
                    message = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hold_invoice_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_hold_invoice_steps_hold_invoices_hold_invoice_id",
                        column: x => x.hold_invoice_id,
                        principalSchema: "app",
                        principalTable: "hold_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoice_steps_correlation_id",
                schema: "app",
                table: "hold_invoice_steps",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoice_steps_hold_invoice_id_occurred_at",
                schema: "app",
                table: "hold_invoice_steps",
                columns: new[] { "hold_invoice_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoice_steps_merchant_id_occurred_at",
                schema: "app",
                table: "hold_invoice_steps",
                columns: new[] { "merchant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoices_idempotency_key",
                schema: "app",
                table: "hold_invoices",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoices_payment_session_id_sequence_no",
                schema: "app",
                table: "hold_invoices",
                columns: new[] { "payment_session_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoices_provider_order_id",
                schema: "app",
                table: "hold_invoices",
                column: "provider_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoices_provider_receipt_id",
                schema: "app",
                table: "hold_invoices",
                column: "provider_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoices_status_next_attempt_at",
                schema: "app",
                table: "hold_invoices",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_correlation_id",
                schema: "app",
                table: "payment_sessions",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_merchant_id",
                schema: "app",
                table: "payment_sessions",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_session_id",
                schema: "app",
                table: "payment_sessions",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_status",
                schema: "app",
                table: "payment_sessions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hold_invoice_steps",
                schema: "app");

            migrationBuilder.DropTable(
                name: "hold_invoices",
                schema: "app");

            migrationBuilder.DropTable(
                name: "payment_sessions",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "funding_source",
                schema: "app",
                table: "product_processes");

            migrationBuilder.DropColumn(
                name: "payment_session_id",
                schema: "app",
                table: "product_processes");

            migrationBuilder.DropColumn(
                name: "payme_cashbox_id",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "payme_enabled",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "payme_key",
                schema: "app",
                table: "merchants");
        }
    }
}
