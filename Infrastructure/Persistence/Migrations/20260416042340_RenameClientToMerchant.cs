using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameClientToMerchant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clients",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "merchants",
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
                    table.PrimaryKey("PK_merchants", x => x.id);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merchants",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bank_account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    company_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    inn = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
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
        }
    }
}
