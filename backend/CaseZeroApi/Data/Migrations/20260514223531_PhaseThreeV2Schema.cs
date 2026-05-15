using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseZeroApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class PhaseThreeV2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "CaseSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestPayloadJson",
                table: "CaseSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAttachmentOverrides",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiredRuleIds",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiredTemporalEventIds",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notifications",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevealedSuspectIds",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspectAlibiVerified",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspectStatusOverrides",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyntheticEmails",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "CaseSubmissions");

            migrationBuilder.DropColumn(
                name: "RequestPayloadJson",
                table: "CaseSubmissions");

            migrationBuilder.DropColumn(
                name: "EmailAttachmentOverrides",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "FiredRuleIds",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "FiredTemporalEventIds",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "Notifications",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "RevealedSuspectIds",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "SuspectAlibiVerified",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "SuspectStatusOverrides",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "SyntheticEmails",
                table: "CaseSessions");
        }
    }
}
