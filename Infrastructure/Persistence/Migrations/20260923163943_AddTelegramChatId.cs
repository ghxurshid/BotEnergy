using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramChatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "telegram_chat_id",
                schema: "auth",
                table: "customer_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_telegram_chat_id",
                schema: "auth",
                table: "customer_users",
                column: "telegram_chat_id",
                unique: true,
                filter: "is_deleted = false AND telegram_chat_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customer_users_telegram_chat_id",
                schema: "auth",
                table: "customer_users");

            migrationBuilder.DropColumn(
                name: "telegram_chat_id",
                schema: "auth",
                table: "customer_users");
        }
    }
}
