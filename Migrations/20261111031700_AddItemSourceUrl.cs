using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SurviveWiki.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSourceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Items",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Items");
        }
    }
}
