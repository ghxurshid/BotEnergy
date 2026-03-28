using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RolePermissionAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RoleId",
                schema: "auth",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_RoleId",
                schema: "auth",
                table: "users",
                column: "RoleId",
                principalSchema: "auth",
                principalTable: "roles",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "auth",
                table: "users");
        }
    }
}
