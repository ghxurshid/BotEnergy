using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixUserTypeDiscriminatorToNativeEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // integer dan auth.user_type native enum ga o'tkazish
            migrationBuilder.Sql(@"
                ALTER TABLE auth.users
                ALTER COLUMN user_type TYPE auth.user_type
                USING (
                    CASE user_type
                        WHEN 0 THEN 'natural_person'::auth.user_type
                        WHEN 1 THEN 'legal_entity'::auth.user_type
                    END
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE auth.users
                ALTER COLUMN user_type TYPE integer
                USING (
                    CASE user_type::text
                        WHEN 'natural_person' THEN 0
                        WHEN 'legal_entity' THEN 1
                    END
                );
            ");
        }
    }
}
