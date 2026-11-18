using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZohoAmarco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingStartedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingStartedAtUtc",
                table: "fiscalization_requests",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingStartedAtUtc",
                table: "fiscalization_requests");
        }
    }
}
