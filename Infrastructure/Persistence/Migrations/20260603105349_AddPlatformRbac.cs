using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role,platform_role")
                .Annotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person,platform")
                .OldAnnotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role")
                .OldAnnotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role")
                .Annotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person")
                .OldAnnotation("Npgsql:Enum:auth.role_type", "natural_role,legal_role,merchant_role,platform_role")
                .OldAnnotation("Npgsql:Enum:auth.user_type", "natural_person,legal_entity,merchant_person,platform");
        }
    }
}
