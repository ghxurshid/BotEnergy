using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <summary>
    /// Sessiya to'lovi strategiyaga (Merchant / Invoice / Subscribe) o'tkazildi.
    ///
    /// DIQQAT: EF bu migratsiyani DropTable + CreateTable ko'rinishida generatsiya qiladi
    /// (u jadval nomi o'zgarganini ko'ra olmaydi). Bu mavjud to'lov yozuvlarini yo'q qilardi,
    /// shuning uchun jadval/ustun/indeks/cheklov nomlari QO'LDA rename qilingan —
    /// ma'lumot joyida qoladi, faqat yangi ustunlar qo'shiladi.
    ///
    /// Mavjud qatorlar uchun default'lar to'g'ri: eski oqimdagi barcha to'lovlar
    /// Subscribe (method=2) + Hold (kind=0) edi.
    /// </summary>
    public partial class AddPaymentStrategies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═══════════════ 1. Nomlarni umumlashtirish (ma'lumot saqlanadi) ═══════════════

            // Eski (usulsiz) watcher indeksi — o'rniga (method, status, next_attempt_at) tushadi.
            // Jadval rename'idan OLDIN o'chiramiz: keyin ham nomi eski bo'lib qolardi.
            migrationBuilder.DropIndex(
                name: "IX_hold_invoices_status_next_attempt_at",
                schema: "app",
                table: "hold_invoices");

            migrationBuilder.RenameTable(
                name: "hold_invoices",
                schema: "app",
                newName: "payment_intents",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "hold_invoice_steps",
                schema: "app",
                newName: "payment_intent_steps",
                newSchema: "app");

            migrationBuilder.RenameColumn(
                name: "hold_balance_tiyin",
                schema: "app",
                table: "payment_sessions",
                newName: "funded_tiyin");

            migrationBuilder.RenameColumn(
                name: "hold_at",
                schema: "app",
                table: "payment_intents",
                newName: "funded_at");

            migrationBuilder.RenameColumn(
                name: "hold_invoice_id",
                schema: "app",
                table: "payment_intent_steps",
                newName: "payment_intent_id");

            // PostgreSQL jadval bilan birga indekslarni qayta nomlamaydi.
            migrationBuilder.RenameIndex(
                name: "IX_hold_invoices_idempotency_key",
                schema: "app",
                table: "payment_intents",
                newName: "IX_payment_intents_idempotency_key");

            migrationBuilder.RenameIndex(
                name: "IX_hold_invoices_payment_session_id_sequence_no",
                schema: "app",
                table: "payment_intents",
                newName: "IX_payment_intents_payment_session_id_sequence_no");

            migrationBuilder.RenameIndex(
                name: "IX_hold_invoices_provider_order_id",
                schema: "app",
                table: "payment_intents",
                newName: "IX_payment_intents_provider_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_hold_invoices_provider_receipt_id",
                schema: "app",
                table: "payment_intents",
                newName: "IX_payment_intents_provider_receipt_id");

            migrationBuilder.RenameIndex(
                name: "IX_hold_invoice_steps_correlation_id",
                schema: "app",
                table: "payment_intent_steps",
                newName: "IX_payment_intent_steps_correlation_id");

            migrationBuilder.RenameIndex(
                name: "IX_hold_invoice_steps_hold_invoice_id_occurred_at",
                schema: "app",
                table: "payment_intent_steps",
                newName: "IX_payment_intent_steps_payment_intent_id_occurred_at");

            migrationBuilder.RenameIndex(
                name: "IX_hold_invoice_steps_merchant_id_occurred_at",
                schema: "app",
                table: "payment_intent_steps",
                newName: "IX_payment_intent_steps_merchant_id_occurred_at");

            // Cheklov nomlari: PG'da RENAME CONSTRAINT bor, EF'da yo'q. Drop+Create qilsak
            // indeks qayta quriladi va FK butun jadvalni tekshiradi — rename esa metadata amali.
            migrationBuilder.Sql(
                """
                ALTER TABLE app.payment_intents
                    RENAME CONSTRAINT "PK_hold_invoices" TO "PK_payment_intents";
                ALTER TABLE app.payment_intent_steps
                    RENAME CONSTRAINT "PK_hold_invoice_steps" TO "PK_payment_intent_steps";
                ALTER TABLE app.payment_intents
                    RENAME CONSTRAINT "FK_hold_invoices_payment_sessions_payment_session_id"
                    TO "FK_payment_intents_payment_sessions_payment_session_id";
                ALTER TABLE app.payment_intent_steps
                    RENAME CONSTRAINT "FK_hold_invoice_steps_hold_invoices_hold_invoice_id"
                    TO "FK_payment_intent_steps_payment_intents_payment_intent_id";
                """);

            // ═══════════════ 2. Merchant: strategiya sozlamalari ═══════════════

            migrationBuilder.AddColumn<int>(
                name: "default_payment_method",
                schema: "app",
                table: "merchants",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "enabled_payment_methods",
                schema: "app",
                table: "merchants",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<bool>(
                name: "refund_unused_funds",
                schema: "app",
                table: "merchants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Merchant API (Payme bizga callback qiladi) — kassa credential'laridan alohida.
            migrationBuilder.AddColumn<string>(
                name: "payme_merchant_id",
                schema: "app",
                table: "merchants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payme_merchant_key",
                schema: "app",
                table: "merchants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // ═══════════════ 3. Payment session: qotirilgan usul ═══════════════

            migrationBuilder.AddColumn<int>(
                name: "method",
                schema: "app",
                table: "payment_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            // ═══════════════ 4. Saqlangan kartalar (Subscribe tokenlari) ═══════════════
            // Token merchant kassasiga bog'langan — yozuv (user, merchant) juftligiga tegishli.

            migrationBuilder.CreateTable(
                name: "customer_cards",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    merchant_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    masked_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    expire = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    card_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    verified_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_cards", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_cards_customer_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "customer_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_cards_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "app",
                        principalTable: "merchants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ═══════════════ 5. Payment intent: usul, tur va provider maydonlari ═══════════════

            migrationBuilder.AddColumn<int>(
                name: "method",
                schema: "app",
                table: "payment_intents",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "kind",
                schema: "app",
                table: "payment_intents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "checkout_url",
                schema: "app",
                table: "payment_intents",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "customer_card_id",
                schema: "app",
                table: "payment_intents",
                type: "bigint",
                nullable: true);

            // Merchant API tranzaksiya maydonlari (alohida jadval ochilmagan —
            // FIFO consume va settlement mantiqi bitta jadvalda qolsin).
            migrationBuilder.AddColumn<string>(
                name: "provider_transaction_id",
                schema: "app",
                table: "payment_intents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "provider_transaction_time",
                schema: "app",
                table: "payment_intents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "provider_created_at",
                schema: "app",
                table: "payment_intents",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "provider_performed_at",
                schema: "app",
                table: "payment_intents",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "provider_cancelled_at",
                schema: "app",
                table: "payment_intents",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "provider_cancel_reason",
                schema: "app",
                table: "payment_intents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_intents_customer_cards_customer_card_id",
                schema: "app",
                table: "payment_intents",
                column: "customer_card_id",
                principalSchema: "app",
                principalTable: "customer_cards",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ═══════════════ 6. Yangi indekslar ═══════════════

            migrationBuilder.CreateIndex(
                name: "IX_customer_cards_merchant_id_token",
                schema: "app",
                table: "customer_cards",
                columns: new[] { "merchant_id", "token" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_customer_cards_user_id_merchant_id_is_default",
                schema: "app",
                table: "customer_cards",
                columns: new[] { "user_id", "merchant_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_customer_card_id",
                schema: "app",
                table: "payment_intents",
                column: "customer_card_id");

            // Watcher hot-path: har strategiya faqat o'z usulidagi navbatni oladi.
            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_method_status_next_attempt_at",
                schema: "app",
                table: "payment_intents",
                columns: new[] { "method", "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_provider_transaction_id",
                schema: "app",
                table: "payment_intents",
                column: "provider_transaction_id");

            // FinalizeSettledAsync: Settling sessiyalarni usul bo'yicha ajratadi.
            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_status_method",
                schema: "app",
                table: "payment_sessions",
                columns: new[] { "status", "method" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ═══════════════ 1. Yangi indekslar, FK va ustunlar ═══════════════

            migrationBuilder.DropIndex(
                name: "IX_payment_sessions_status_method",
                schema: "app",
                table: "payment_sessions");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_provider_transaction_id",
                schema: "app",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_method_status_next_attempt_at",
                schema: "app",
                table: "payment_intents");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_intents_customer_cards_customer_card_id",
                schema: "app",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_payment_intents_customer_card_id",
                schema: "app",
                table: "payment_intents");

            migrationBuilder.DropTable(
                name: "customer_cards",
                schema: "app");

            foreach (var column in new[]
                     {
                         "method", "kind", "checkout_url", "customer_card_id",
                         "provider_transaction_id", "provider_transaction_time",
                         "provider_created_at", "provider_performed_at",
                         "provider_cancelled_at", "provider_cancel_reason"
                     })
            {
                migrationBuilder.DropColumn(name: column, schema: "app", table: "payment_intents");
            }

            migrationBuilder.DropColumn(
                name: "method",
                schema: "app",
                table: "payment_sessions");

            foreach (var column in new[]
                     {
                         "default_payment_method", "enabled_payment_methods",
                         "refund_unused_funds", "payme_merchant_id", "payme_merchant_key"
                     })
            {
                migrationBuilder.DropColumn(name: column, schema: "app", table: "merchants");
            }

            // ═══════════════ 2. Nomlarni eski holatga qaytarish ═══════════════

            migrationBuilder.Sql(
                """
                ALTER TABLE app.payment_intent_steps
                    RENAME CONSTRAINT "FK_payment_intent_steps_payment_intents_payment_intent_id"
                    TO "FK_hold_invoice_steps_hold_invoices_hold_invoice_id";
                ALTER TABLE app.payment_intents
                    RENAME CONSTRAINT "FK_payment_intents_payment_sessions_payment_session_id"
                    TO "FK_hold_invoices_payment_sessions_payment_session_id";
                ALTER TABLE app.payment_intent_steps
                    RENAME CONSTRAINT "PK_payment_intent_steps" TO "PK_hold_invoice_steps";
                ALTER TABLE app.payment_intents
                    RENAME CONSTRAINT "PK_payment_intents" TO "PK_hold_invoices";
                """);

            migrationBuilder.RenameIndex(
                name: "IX_payment_intent_steps_merchant_id_occurred_at",
                schema: "app",
                table: "payment_intent_steps",
                newName: "IX_hold_invoice_steps_merchant_id_occurred_at");

            migrationBuilder.RenameIndex(
                name: "IX_payment_intent_steps_payment_intent_id_occurred_at",
                schema: "app",
                table: "payment_intent_steps",
                newName: "IX_hold_invoice_steps_hold_invoice_id_occurred_at");

            migrationBuilder.RenameIndex(
                name: "IX_payment_intent_steps_correlation_id",
                schema: "app",
                table: "payment_intent_steps",
                newName: "IX_hold_invoice_steps_correlation_id");

            migrationBuilder.RenameIndex(
                name: "IX_payment_intents_provider_receipt_id",
                schema: "app",
                table: "payment_intents",
                newName: "IX_hold_invoices_provider_receipt_id");

            migrationBuilder.RenameIndex(
                name: "IX_payment_intents_provider_order_id",
                schema: "app",
                table: "payment_intents",
                newName: "IX_hold_invoices_provider_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_payment_intents_payment_session_id_sequence_no",
                schema: "app",
                table: "payment_intents",
                newName: "IX_hold_invoices_payment_session_id_sequence_no");

            migrationBuilder.RenameIndex(
                name: "IX_payment_intents_idempotency_key",
                schema: "app",
                table: "payment_intents",
                newName: "IX_hold_invoices_idempotency_key");

            migrationBuilder.RenameColumn(
                name: "payment_intent_id",
                schema: "app",
                table: "payment_intent_steps",
                newName: "hold_invoice_id");

            migrationBuilder.RenameColumn(
                name: "funded_at",
                schema: "app",
                table: "payment_intents",
                newName: "hold_at");

            migrationBuilder.RenameColumn(
                name: "funded_tiyin",
                schema: "app",
                table: "payment_sessions",
                newName: "hold_balance_tiyin");

            migrationBuilder.RenameTable(
                name: "payment_intent_steps",
                schema: "app",
                newName: "hold_invoice_steps",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "payment_intents",
                schema: "app",
                newName: "hold_invoices",
                newSchema: "app");

            migrationBuilder.CreateIndex(
                name: "IX_hold_invoices_status_next_attempt_at",
                schema: "app",
                table: "hold_invoices",
                columns: new[] { "status", "next_attempt_at" });
        }
    }
}
