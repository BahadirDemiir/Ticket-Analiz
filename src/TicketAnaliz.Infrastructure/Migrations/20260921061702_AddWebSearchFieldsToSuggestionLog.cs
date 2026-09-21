using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketAnaliz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebSearchFieldsToSuggestionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnswerSource",
                table: "SuggestionLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "HistoricalTickets");

            migrationBuilder.AddColumn<int>(
                name: "WebSearchAcceptedCount",
                table: "SuggestionLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WebSearchDurationMs",
                table: "SuggestionLogs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebSearchQuery",
                table: "SuggestionLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WebSearchResultCount",
                table: "SuggestionLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebSourcesJson",
                table: "SuggestionLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            // Web fallback'ten onceki kayitlarda BT'ye yonlendirilmis olanlar "Escalated" sayilsin.
            migrationBuilder.Sql("UPDATE SuggestionLogs SET AnswerSource = 'Escalated' WHERE ShouldEscalate = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerSource",
                table: "SuggestionLogs");

            migrationBuilder.DropColumn(
                name: "WebSearchAcceptedCount",
                table: "SuggestionLogs");

            migrationBuilder.DropColumn(
                name: "WebSearchDurationMs",
                table: "SuggestionLogs");

            migrationBuilder.DropColumn(
                name: "WebSearchQuery",
                table: "SuggestionLogs");

            migrationBuilder.DropColumn(
                name: "WebSearchResultCount",
                table: "SuggestionLogs");

            migrationBuilder.DropColumn(
                name: "WebSourcesJson",
                table: "SuggestionLogs");
        }
    }
}
