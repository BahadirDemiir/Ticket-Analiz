using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketAnaliz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioKeyAndVectorStoreFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInVectorStore",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScenarioKey",
                table: "Tickets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ScenarioKey",
                table: "Tickets",
                column: "ScenarioKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_ScenarioKey",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsInVectorStore",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ScenarioKey",
                table: "Tickets");
        }
    }
}
