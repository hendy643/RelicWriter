using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RelicWriter.Common.GameData.Storage.Migrations
{
    /// <inheritdoc />
    public partial class PerkAttributePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attribute_points",
                table: "perk",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attribute_points",
                table: "perk");
        }
    }
}
