using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelicWriter.Common.GameData.Storage;

/// <summary>
/// A data migration: runs the SQL script of the same name from the Database folder (compiled into the app). The script is written by
/// <c>RelicWriter.Extractor gamedata</c>, which also writes the one-line class; a released migration is never edited. Data is only added or replaced, never deleted.
/// </summary>
internal abstract class SqlScriptMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GameDatabase.Script(GetType().GetCustomAttribute<MigrationAttribute>()!.Id));
}
