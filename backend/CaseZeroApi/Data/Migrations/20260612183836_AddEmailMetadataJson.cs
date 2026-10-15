using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseZeroApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailMetadataJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Emails",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Emails");
        }
    }
}
