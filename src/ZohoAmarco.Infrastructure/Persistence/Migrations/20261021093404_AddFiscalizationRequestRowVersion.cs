using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZohoAmarco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalizationRequestRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "fiscalization_requests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "fiscalization_requests");
        }
    }
}
