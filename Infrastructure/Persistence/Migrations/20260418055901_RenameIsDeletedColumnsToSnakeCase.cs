using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameIsDeletedColumnsToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "auth",
                table: "users",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "auth",
                table: "user_roles",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "app",
                table: "stations",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "auth",
                table: "roles",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "auth",
                table: "role_permissions",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "app",
                table: "products",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "auth",
                table: "permissions",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "auth",
                table: "organizations",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "app",
                table: "merchants",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "app",
                table: "devices",
                newName: "is_deleted");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "app",
                table: "usage_sessions",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "auth",
                table: "users",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "auth",
                table: "user_roles",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "app",
                table: "stations",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "auth",
                table: "roles",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "auth",
                table: "role_permissions",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "app",
                table: "products",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "auth",
                table: "permissions",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "auth",
                table: "organizations",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "app",
                table: "merchants",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "app",
                table: "devices",
                newName: "IsDeleted");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "app",
                table: "usage_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}
