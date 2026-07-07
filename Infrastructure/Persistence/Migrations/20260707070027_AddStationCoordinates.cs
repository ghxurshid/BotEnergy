using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStationCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location",
                schema: "app",
                table: "stations");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "app",
                table: "stations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Point>(
                name: "coordinates",
                schema: "app",
                table: "stations",
                type: "geography(Point,4326)",
                nullable: false,
                defaultValueSql: "ST_SetSRID(ST_MakePoint(0,0),4326)");

            migrationBuilder.CreateIndex(
                name: "IX_stations_coordinates",
                schema: "app",
                table: "stations",
                column: "coordinates")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stations_coordinates",
                schema: "app",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "address",
                schema: "app",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "coordinates",
                schema: "app",
                table: "stations");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<string>(
                name: "location",
                schema: "app",
                table: "stations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
