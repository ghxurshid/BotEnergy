using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleDiscriminator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role")
                .Annotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person")
                .OldAnnotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person");

            migrationBuilder.AddColumn<long>(
                name: "merchant_id",
                schema: "auth",
                table: "roles",
                type: "bigint",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_roles_merchant_id",
                schema: "auth",
                table: "roles",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_organization_id",
                schema: "auth",
                table: "roles",
                column: "organization_id");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_merchants_merchant_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "FK_roles_organizations_organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_merchant_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "role_type",
                schema: "auth",
                table: "roles");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person")
                .OldAnnotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role")
                .OldAnnotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person");
        }
    }
}
