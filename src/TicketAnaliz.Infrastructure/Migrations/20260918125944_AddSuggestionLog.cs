using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketAnaliz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuggestionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidencePercentage = table.Column<double>(type: "float", nullable: false),
                    ShouldEscalate = table.Column<bool>(type: "bit", nullable: false),
                    ConfidenceAverageSimilarity = table.Column<double>(type: "float", nullable: false),
                    ConfidenceResolvedRatio = table.Column<double>(type: "float", nullable: false),
                    ConfidenceSourceCountFactor = table.Column<double>(type: "float", nullable: false),
                    HasHallucination = table.Column<bool>(type: "bit", nullable: true),
                    HallucinationExplanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchDurationMs = table.Column<double>(type: "float", nullable: false),
                    GenerationDurationMs = table.Column<double>(type: "float", nullable: true),
                    HallucinationCheckDurationMs = table.Column<double>(type: "float", nullable: true),
                    TotalDurationMs = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuggestionLogs_CreatedAt",
                table: "SuggestionLogs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuggestionLogs");
        }
    }
}
