using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanitasStudios_WebApp.Migrations
{
    /// <inheritdoc />
    public partial class IsPinnedToContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Content",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Content");
        }
    }
}
