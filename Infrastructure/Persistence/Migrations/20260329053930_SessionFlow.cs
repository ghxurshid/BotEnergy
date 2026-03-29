using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_organizations_organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_usage_sessions_user_id",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropIndex(
                name: "IX_roles_organization_id_name",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "quantity",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                schema: "auth",
                table: "users",
                newName: "role_id");

            migrationBuilder.RenameIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users",
                newName: "IX_users_role_id");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "app",
                table: "usage_sessions",
                newName: "is_deleted");

            migrationBuilder.AlterColumn<decimal>(
                name: "price",
                schema: "app",
                table: "usage_sessions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<long>(
                name: "device_id",
                schema: "app",
                table: "usage_sessions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "app",
                table: "usage_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<decimal>(
                name: "delivered_quantity",
                schema: "app",
                table: "usage_sessions",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "device_connected_at",
                schema: "app",
                table: "usage_sessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_at",
                schema: "app",
                table: "usage_sessions",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "LOCALTIMESTAMP");

            migrationBuilder.AddColumn<decimal>(
                name: "requested_quantity",
                schema: "app",
                table: "usage_sessions",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "session_token",
                schema: "app",
                table: "usage_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "app",
                table: "usage_sessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "permission",
                schema: "auth",
                table: "role_permissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "session_progresses",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reported_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "LOCALTIMESTAMP"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                name: "IX_usage_sessions_last_activity_at",
                schema: "app",
                table: "usage_sessions",
                column: "last_activity_at");

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

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                schema: "auth",
                table: "roles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_roles_organization_id",
                schema: "auth",
                table: "roles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_progresses_session_id",
                schema: "app",
                table: "session_progresses",
                column: "session_id");

            migrationBuilder.AddForeignKey(
                name: "FK_roles_organizations_organization_id",
                schema: "auth",
                table: "roles",
                column: "organization_id",
                principalSchema: "auth",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_role_id",
                schema: "auth",
                table: "users",
                column: "role_id",
                principalSchema: "auth",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_organizations_organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_role_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropTable(
                name: "session_progresses",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_usage_sessions_last_activity_at",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropIndex(
                name: "IX_usage_sessions_session_token",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropIndex(
                name: "IX_usage_sessions_user_id_status",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropIndex(
                name: "IX_roles_name",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_organization_id",
                schema: "auth",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "delivered_quantity",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "device_connected_at",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "last_activity_at",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "requested_quantity",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "session_token",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "app",
                table: "usage_sessions");

            migrationBuilder.RenameColumn(
                name: "role_id",
                schema: "auth",
                table: "users",
                newName: "RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_users_role_id",
                schema: "auth",
                table: "users",
                newName: "IX_users_RoleId");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "app",
                table: "usage_sessions",
                newName: "IsDeleted");

            migrationBuilder.AlterColumn<decimal>(
                name: "price",
                schema: "app",
                table: "usage_sessions",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<long>(
                name: "device_id",
                schema: "app",
                table: "usage_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "app",
                table: "usage_sessions",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                schema: "app",
                table: "usage_sessions",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "permission",
                schema: "auth",
                table: "role_permissions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_usage_sessions_user_id",
                schema: "app",
                table: "usage_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_organization_id_name",
                schema: "auth",
                table: "roles",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_roles_organizations_organization_id",
                schema: "auth",
                table: "roles",
                column: "organization_id",
                principalSchema: "auth",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_RoleId",
                schema: "auth",
                table: "users",
                column: "RoleId",
                principalSchema: "auth",
                principalTable: "roles",
                principalColumn: "id");
        }
    }
}
