using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneFormatCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_users_phone_format",
                schema: "auth",
                table: "platform_users",
                sql: "phone_number ~ '^998[0-9]{9}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_organizations_phone_format",
                schema: "auth",
                table: "organizations",
                sql: "phone_number ~ '^998[0-9]{9}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_merchants_phone_format",
                schema: "app",
                table: "merchants",
                sql: "phone_number ~ '^998[0-9]{9}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_customer_users_phone_format",
                schema: "auth",
                table: "customer_users",
                sql: "phone_number ~ '^998[0-9]{9}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_users_phone_format",
                schema: "auth",
                table: "platform_users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_organizations_phone_format",
                schema: "auth",
                table: "organizations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_merchants_phone_format",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_customer_users_phone_format",
                schema: "auth",
                table: "customer_users");
        }
    }
}
