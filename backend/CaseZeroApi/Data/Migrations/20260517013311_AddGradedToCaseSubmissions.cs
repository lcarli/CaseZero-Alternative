using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseZeroApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGradedToCaseSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Graded",
                table: "CaseSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Graded",
                table: "CaseSubmissions");
        }
    }
}
