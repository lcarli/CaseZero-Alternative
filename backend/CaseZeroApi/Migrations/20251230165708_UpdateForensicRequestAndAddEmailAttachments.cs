using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseZeroApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateForensicRequestAndAddEmailAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EvidenceName",
                table: "ForensicRequests",
                newName: "InputAssetName");

            migrationBuilder.RenameColumn(
                name: "EvidenceId",
                table: "ForensicRequests",
                newName: "InputAssetId");

            migrationBuilder.AddColumn<string>(
                name: "ResultEmailId",
                table: "ForensicRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailAttachmentsDownloaded",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAttachmentsDownloaded", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAttachmentsDownloaded_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachmentsDownloaded_UserId",
                table: "EmailAttachmentsDownloaded",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAttachmentsDownloaded");

            migrationBuilder.DropColumn(
                name: "ResultEmailId",
                table: "ForensicRequests");

            migrationBuilder.RenameColumn(
                name: "InputAssetName",
                table: "ForensicRequests",
                newName: "EvidenceName");

            migrationBuilder.RenameColumn(
                name: "InputAssetId",
                table: "ForensicRequests",
                newName: "EvidenceId");
        }
    }
}
