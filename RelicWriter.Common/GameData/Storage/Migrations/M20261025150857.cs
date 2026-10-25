using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RelicWriter.Common.GameData.Storage.Migrations;

[DbContext(typeof(GameDbContext)), Migration("20261025150857_Game_2.5")]
internal sealed class M20261025150857 : SqlScriptMigration;