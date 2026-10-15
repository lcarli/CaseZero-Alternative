using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseZeroApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBreakdownAndRankHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AnalysisScore",
                table: "CaseSubmissions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CulpritScore",
                table: "CaseSubmissions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EvidenceScore",
                table: "CaseSubmissions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "QuestionsScore",
                table: "CaseSubmissions",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserRankHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PreviousRank = table.Column<int>(type: "int", nullable: true),
                    NewRank = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRankHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRankHistories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRankHistories_UserId_ChangedAt",
                table: "UserRankHistories",
                columns: new[] { "UserId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRankHistories");

            migrationBuilder.DropColumn(
                name: "AnalysisScore",
                table: "CaseSubmissions");

            migrationBuilder.DropColumn(
                name: "CulpritScore",
                table: "CaseSubmissions");

            migrationBuilder.DropColumn(
                name: "EvidenceScore",
                table: "CaseSubmissions");

            migrationBuilder.DropColumn(
                name: "QuestionsScore",
                table: "CaseSubmissions");
        }
    }
}
