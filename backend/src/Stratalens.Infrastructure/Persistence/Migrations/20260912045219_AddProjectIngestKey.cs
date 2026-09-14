using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stratalens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIngestKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IngestKeyHash",
                table: "Projects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_IngestKeyHash",
                table: "Projects",
                column: "IngestKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_IngestKeyHash",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IngestKeyHash",
                table: "Projects");
        }
    }
}
