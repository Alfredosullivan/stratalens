using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stratalens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RepositoryName",
                table: "Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepositoryOwner",
                table: "Projects",
                type: "character varying(39)",
                maxLength: 39,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepositoryReference",
                table: "Projects",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepositoryName",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RepositoryOwner",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RepositoryReference",
                table: "Projects");
        }
    }
}
