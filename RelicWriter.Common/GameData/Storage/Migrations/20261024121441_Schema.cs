using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RelicWriter.Common.GameData.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "constant",
                columns: table => new
                {
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_constant", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "enum_default",
                columns: table => new
                {
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enum_default", x => x.type);
                });

            migrationBuilder.CreateTable(
                name: "meta",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meta", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "perk",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    tree = table.Column<string>(type: "TEXT", nullable: false),
                    sort = table.Column<int>(type: "INTEGER", nullable: false),
                    max_level = table.Column<int>(type: "INTEGER", nullable: false),
                    cost_per_level = table.Column<int>(type: "INTEGER", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perk", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perk_tree",
                columns: table => new
                {
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    sort = table.Column<int>(type: "INTEGER", nullable: false),
                    attribute_data = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perk_tree", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "record",
                columns: table => new
                {
                    hash = table.Column<long>(type: "INTEGER", nullable: false),
                    length = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record", x => new { x.hash, x.length });
                });

            migrationBuilder.CreateTable(
                name: "vehicle",
                columns: table => new
                {
                    record = table.Column<string>(type: "TEXT", nullable: false),
                    sort = table.Column<int>(type: "INTEGER", nullable: false),
                    atlas = table.Column<long>(type: "INTEGER", nullable: true),
                    atlas_part = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle", x => x.record);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "constant");

            migrationBuilder.DropTable(
                name: "enum_default");

            migrationBuilder.DropTable(
                name: "meta");

            migrationBuilder.DropTable(
                name: "perk");

            migrationBuilder.DropTable(
                name: "perk_tree");

            migrationBuilder.DropTable(
                name: "record");

            migrationBuilder.DropTable(
                name: "vehicle");
        }
    }
}
