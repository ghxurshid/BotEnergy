using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_progresses",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "product_type",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "app",
                table: "products");

            migrationBuilder.RenameColumn(
                name: "price",
                schema: "app",
                table: "usage_sessions",
                newName: "price_per_unit");

            migrationBuilder.AddColumn<string>(
                name: "device_serial_number",
                schema: "app",
                table: "usage_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "end_reason",
                schema: "app",
                table: "usage_sessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "product_id",
                schema: "app",
                table: "usage_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_name",
                schema: "app",
                table: "usage_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "unit",
                schema: "app",
                table: "usage_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_phone_number",
                schema: "app",
                table: "usage_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "app",
                table: "products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "device_id",
                schema: "app",
                table: "products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "device_type",
                schema: "app",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "function_count",
                schema: "app",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_product_id",
                schema: "app",
                table: "usage_sessions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_device_id",
                schema: "app",
                table: "products",
                column: "device_id");

            migrationBuilder.AddForeignKey(
                name: "FK_products_devices_device_id",
                schema: "app",
                table: "products",
                column: "device_id",
                principalSchema: "app",
                principalTable: "devices",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_usage_sessions_products_product_id",
                schema: "app",
                table: "usage_sessions",
                column: "product_id",
                principalSchema: "app",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_devices_device_id",
                schema: "app",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_usage_sessions_products_product_id",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropIndex(
                name: "IX_usage_sessions_product_id",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropIndex(
                name: "IX_products_device_id",
                schema: "app",
                table: "products");

            migrationBuilder.DropColumn(
                name: "device_serial_number",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "end_reason",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "product_id",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "product_name",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "unit",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "user_phone_number",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "app",
                table: "products");

            migrationBuilder.DropColumn(
                name: "device_id",
                schema: "app",
                table: "products");

            migrationBuilder.DropColumn(
                name: "device_type",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "function_count",
                schema: "app",
                table: "devices");

            migrationBuilder.RenameColumn(
                name: "price_per_unit",
                schema: "app",
                table: "usage_sessions",
                newName: "price");

            migrationBuilder.AddColumn<string>(
                name: "product_type",
                schema: "app",
                table: "usage_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "app",
                table: "products",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "LOCALTIMESTAMP");

            migrationBuilder.CreateTable(
                name: "session_progresses",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reported_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    total_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_progresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_progresses_usage_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "app",
                        principalTable: "usage_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_progresses_session_id",
                schema: "app",
                table: "session_progresses",
                column: "session_id");
        }
    }
}
