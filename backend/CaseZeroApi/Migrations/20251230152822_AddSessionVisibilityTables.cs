using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseZeroApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionVisibilityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CaseSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CaseSessionEmailStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenCount = table.Column<int>(type: "int", nullable: false),
                    CaseSessionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseSessionEmailStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseSessionEmailStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseSessionEmailStates_CaseSessions_CaseSessionId",
                        column: x => x.CaseSessionId,
                        principalTable: "CaseSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CaseSessionVisibleAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CaseSessionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseSessionVisibleAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseSessionVisibleAssets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseSessionVisibleAssets_CaseSessions_CaseSessionId",
                        column: x => x.CaseSessionId,
                        principalTable: "CaseSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CaseSessionVisibleEmails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CaseSessionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseSessionVisibleEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseSessionVisibleEmails_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseSessionVisibleEmails_CaseSessions_CaseSessionId",
                        column: x => x.CaseSessionId,
                        principalTable: "CaseSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseSessionEmailStates_CaseSessionId",
                table: "CaseSessionEmailStates",
                column: "CaseSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseSessionEmailStates_UserId",
                table: "CaseSessionEmailStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseSessionVisibleAssets_CaseSessionId",
                table: "CaseSessionVisibleAssets",
                column: "CaseSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseSessionVisibleAssets_UserId",
                table: "CaseSessionVisibleAssets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseSessionVisibleEmails_CaseSessionId",
                table: "CaseSessionVisibleEmails",
                column: "CaseSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseSessionVisibleEmails_UserId",
                table: "CaseSessionVisibleEmails",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseSessionEmailStates");

            migrationBuilder.DropTable(
                name: "CaseSessionVisibleAssets");

            migrationBuilder.DropTable(
                name: "CaseSessionVisibleEmails");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CaseSessions");
        }
    }
}
