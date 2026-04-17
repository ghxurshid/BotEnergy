using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantUserAndRefactorEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "app",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "function_count",
                schema: "app",
                table: "devices");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person")
                .OldAnnotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity");

            migrationBuilder.AddColumn<long>(
                name: "station_id",
                schema: "auth",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "merchant_id",
                schema: "app",
                table: "stations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                schema: "auth",
                table: "organizations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "inn",
                schema: "auth",
                table: "organizations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "auth",
                table: "organizations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_station_id",
                schema: "auth",
                table: "users",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_stations_merchant_id",
                schema: "app",
                table: "stations",
                column: "merchant_id");

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
                name: "FK_users_stations_station_id",
                schema: "auth",
                table: "users",
                column: "station_id",
                principalSchema: "app",
                principalTable: "stations",
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

            migrationBuilder.DropForeignKey(
                name: "FK_users_stations_station_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_station_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_stations_merchant_id",
                schema: "app",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "station_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                schema: "app",
                table: "stations");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity")
                .OldAnnotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                schema: "auth",
                table: "organizations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "inn",
                schema: "auth",
                table: "organizations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "auth",
                table: "organizations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "app",
                table: "merchants",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "LOCALTIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "function_count",
                schema: "app",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }
    }
}
