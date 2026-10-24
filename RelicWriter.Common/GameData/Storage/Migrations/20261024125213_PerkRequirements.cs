using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RelicWriter.Common.GameData.Storage.Migrations
{
    /// <inheritdoc />
    public partial class PerkRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "requires",
                table: "perk",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "requires",
                table: "perk");
        }
    }
}
