using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stratalens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNodeParentNodeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentNodeId",
                table: "Nodes",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentNodeId",
                table: "Nodes");
        }
    }
}
