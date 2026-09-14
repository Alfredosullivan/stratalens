using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stratalens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTracesAndSpans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Traces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Spans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpanId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentSpanId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceNode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetNode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Operation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TraceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spans_Traces_TraceId",
                        column: x => x.TraceId,
                        principalTable: "Traces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Spans_TraceId",
                table: "Spans",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_Traces_ProjectId",
                table: "Traces",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Traces_ProjectId_TraceId",
                table: "Traces",
                columns: new[] { "ProjectId", "TraceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Spans");

            migrationBuilder.DropTable(
                name: "Traces");
        }
    }
}
