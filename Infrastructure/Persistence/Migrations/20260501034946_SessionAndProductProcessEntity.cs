using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionAndProductProcessEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usage_sessions",
                schema: "app");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_seen_at",
                schema: "app",
                table: "devices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: true),
                    session_token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    close_reason = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    connected_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessions_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "app",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_processes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price_per_unit = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    requested_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    given_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false),
                    end_reason = table.Column<int>(type: "integer", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    paused_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_balance_deducted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_telemetry_sequence = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_processes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_processes_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "app",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_processes_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "app",
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_processes_product_id",
                schema: "app",
                table: "product_processes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_processes_session_id",
                schema: "app",
                table: "product_processes",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_processes_session_id_status",
                schema: "app",
                table: "product_processes",
                columns: new[] { "session_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_device_id",
                schema: "app",
                table: "sessions",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_last_activity_at",
                schema: "app",
                table: "sessions",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_session_token",
                schema: "app",
                table: "sessions",
                column: "session_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_user_id_status",
                schema: "app",
                table: "sessions",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_processes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "last_seen_at",
                schema: "app",
                table: "devices");

            migrationBuilder.CreateTable(
                name: "usage_sessions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<long>(type: "bigint", nullable: true),
                    product_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    delivered_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    device_connected_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    device_serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    end_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "LOCALTIMESTAMP"),
                    price_per_unit = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requested_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    session_token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit = table.Column<int>(type: "integer", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    user_phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
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
                        name: "FK_usage_sessions_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "app",
                        principalTable: "products",
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
                name: "IX_usage_sessions_device_id",
                schema: "app",
                table: "usage_sessions",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_last_activity_at",
                schema: "app",
                table: "usage_sessions",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_product_id",
                schema: "app",
                table: "usage_sessions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_session_token",
                schema: "app",
                table: "usage_sessions",
                column: "session_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_user_id_status",
                schema: "app",
                table: "usage_sessions",
                columns: new[] { "user_id", "status" });
        }
    }
}
