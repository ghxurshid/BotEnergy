using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payee_type = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    organization_id = table.Column<long>(type: "bigint", nullable: true),
                    initiated_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    provider_receipt_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    provider_order_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_state = table.Column<int>(type: "integer", nullable: true),
                    device_serial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    session_id = table.Column<long>(type: "bigint", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "auth",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_transactions_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "app",
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_payment_transactions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_transaction_steps",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payment_transaction_id = table.Column<long>(type: "bigint", nullable: false),
                    step_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    request_payload = table.Column<string>(type: "jsonb", nullable: true),
                    response_payload = table.Column<string>(type: "jsonb", nullable: true),
                    message = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transaction_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transaction_steps_payment_transactions_payment_tran~",
                        column: x => x.payment_transaction_id,
                        principalSchema: "app",
                        principalTable: "payment_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transaction_steps_payment_transaction_id_occurred_at",
                schema: "app",
                table: "payment_transaction_steps",
                columns: new[] { "payment_transaction_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_initiated_by_user_id_created_date",
                schema: "app",
                table: "payment_transactions",
                columns: new[] { "initiated_by_user_id", "created_date" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_organization_id_status_created_date",
                schema: "app",
                table: "payment_transactions",
                columns: new[] { "organization_id", "status", "created_date" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_provider_order_id",
                schema: "app",
                table: "payment_transactions",
                column: "provider_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_provider_receipt_id",
                schema: "app",
                table: "payment_transactions",
                column: "provider_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_session_id",
                schema: "app",
                table: "payment_transactions",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_status_created_date",
                schema: "app",
                table: "payment_transactions",
                columns: new[] { "status", "created_date" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_user_id_status_created_date",
                schema: "app",
                table: "payment_transactions",
                columns: new[] { "user_id", "status", "created_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transaction_steps",
                schema: "app");

            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "app");
        }
    }
}
