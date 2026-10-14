using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseZeroApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistForensicGameTimeAndTriggerHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "ForensicRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StateVersion",
                table: "ForensicRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "GameClockUpdatedAt",
                table: "CaseSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GameTimeMinutes",
                table: "CaseSessions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ObservedTriggers",
                table: "CaseSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StateVersion",
                table: "CaseSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "ForensicRequests");

            migrationBuilder.DropColumn(
                name: "StateVersion",
                table: "ForensicRequests");

            migrationBuilder.DropColumn(
                name: "GameClockUpdatedAt",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "GameTimeMinutes",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "ObservedTriggers",
                table: "CaseSessions");

            migrationBuilder.DropColumn(
                name: "StateVersion",
                table: "CaseSessions");
        }
    }
}
