using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stratalens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectRepositoryUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerUserId_RepositoryOwner_RepositoryName",
                table: "Projects",
                columns: new[] { "OwnerUserId", "RepositoryOwner", "RepositoryName" },
                unique: true,
                filter: "\"RepositoryOwner\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerUserId_RepositoryOwner_RepositoryName",
                table: "Projects");
        }
    }
}
