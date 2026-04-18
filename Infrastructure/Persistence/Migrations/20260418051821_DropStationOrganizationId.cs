using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropStationOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_merchants_merchant_id",
                schema: "app",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "FK_stations_organizations_organization_id",
                schema: "app",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "IX_stations_merchant_id",
                schema: "app",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "IX_stations_organization_id_name",
                schema: "app",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "app",
                table: "stations");

            migrationBuilder.AlterColumn<long>(
                name: "merchant_id",
                schema: "app",
                table: "stations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stations_merchant_id_name",
                schema: "app",
                table: "stations",
                columns: new[] { "merchant_id", "name" });

            migrationBuilder.AddForeignKey(
                name: "FK_stations_merchants_merchant_id",
                schema: "app",
                table: "stations",
                column: "merchant_id",
                principalSchema: "app",
                principalTable: "merchants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_merchants_merchant_id",
                schema: "app",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "IX_stations_merchant_id_name",
                schema: "app",
                table: "stations");

            migrationBuilder.AlterColumn<long>(
                name: "merchant_id",
                schema: "app",
                table: "stations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "organization_id",
                schema: "app",
                table: "stations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_stations_merchant_id",
                schema: "app",
                table: "stations",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_stations_organization_id_name",
                schema: "app",
                table: "stations",
                columns: new[] { "organization_id", "name" });

            migrationBuilder.AddForeignKey(
                name: "FK_stations_merchants_merchant_id",
                schema: "app",
                table: "stations",
                column: "merchant_id",
                principalSchema: "app",
                principalTable: "merchants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stations_organizations_organization_id",
                schema: "app",
                table: "stations",
                column: "organization_id",
                principalSchema: "auth",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
