using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RelicWriter.Common.GameData;
using RelicWriter.Common.GameData.Storage;
using RelicWriter.Common.Model;
using Xunit;

namespace RelicWriter.Tests;

static class TestData
{
    // Tests use the data compiled into the app, never the database file in the real user's data folder.
    [ModuleInitializer]
    internal static void UseBuiltInData() => GameDatabase.Current = GameDatabase.BuiltIn();
}

/// <summary>The game database and its EF migrations, on small files written here. The built-in data is the migrations compiled into the app.</summary>
public class GameDatabaseTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("relicwriter-tests").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(dir, true);
    }

    string DbPath(string name = GameDatabase.FileName) => Path.Combine(dir, "data", name);

    static GameDbContext Context(string path) => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()).Options);

    static List<string> Applied(string path)
    {
        using var db = Context(path);
        return db.Database.GetAppliedMigrations().ToList();
    }

    static List<string> AllMigrations()
    {
        using var db = Context(":memory:");
        return db.Database.GetMigrations().ToList();
    }

    static void Execute(string path, string sql)
    {
        using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void BuiltInDataComesFromTheMigrations()
    {
        var db = GameDatabase.BuiltIn();
        Assert.Equal(AllMigrations().Count, db.Info.DataVersion);
        Assert.NotEmpty(db.Vehicles);
        Assert.NotEmpty(db.Perks);
        Assert.Equal("BodyAttributeData", db.PerkTrees["Body"]);
        Assert.Equal("Items.money", db.Record(Inventory.MoneyHash, 11)?.Name);
    }

    [Fact]
    public void TheDatabaseFileIsCreatedOnFirstStartAndLoadedAfterwards()
    {
        Assert.False(File.Exists(DbPath()));
        var first = GameDatabase.Load(DbPath());
        Assert.True(File.Exists(DbPath()));
        Assert.Equal(AllMigrations(), Applied(DbPath()));
        var again = GameDatabase.Load(DbPath());
        Assert.Equal(first.Info, again.Info);
        Assert.Equal(first.Perks, again.Perks);
        Assert.Equal(first.Vehicles, again.Vehicles);
        Assert.Equal(first.Records.Count, again.Records.Count);
    }

    [Fact]
    public void AnUpdateRunsOnlyTheMigrationsTheFileHasNotRun()
    {
        var all = AllMigrations();
        // A file made by an earlier version of the app has only the first migrations; starting the app runs the rest.
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath())!);
        using (var old = Context(DbPath())) old.GetService<IMigrator>().Migrate(all[0]);
        Assert.Equal(all[..1], Applied(DbPath()));
        Assert.Equal(all.Count, GameDatabase.Load(DbPath()).Info.DataVersion);
        Assert.Equal(all, Applied(DbPath()));
    }

    [Fact]
    public void AFileMigratedByANewerAppIsRefusedAndTheBuiltInDataIsUsedInstead()
    {
        GameDatabase.Load(DbPath());
        Execute(DbPath(), "INSERT INTO \"__EFMigrationsHistory\" (MigrationId, ProductVersion) VALUES ('99991231235959_Future', '99.0.0')");
        Assert.Equal(DatabaseProblem.TooNew, Assert.Throws<GameDatabaseException>(() => GameDatabase.Migrate(DbPath())).Problem);
        Assert.Equal(AllMigrations().Count, GameDatabase.Load(DbPath()).Info.DataVersion);
    }

    [Fact]
    public void ADamagedFileFallsBackToTheBuiltInData()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath())!);
        File.WriteAllText(DbPath(), "this is not a sqlite database, just text that is long enough to be read as one");
        Assert.Equal(AllMigrations().Count, GameDatabase.Load(DbPath()).Info.DataVersion);
    }

    [Fact]
    public void AMigrationScriptCarriesOnlyWhatChanged()
    {
        var before = GameDatabase.BuiltIn();
        Assert.Null(before.MigrationScript(before));

        var perk = before.Perks[0] with { DisplayName = "Renamed" };
        var records = new Dictionary<(uint, byte), RecordEntry>(before.Records) { [(1, 1)] = new RecordEntry("Items.NewThing", "New Thing") };
        var after = new GameDatabase(before.Info, before.Enums, records, before.Vehicles, before.PerkTrees, before.Perks.Select(p => p.Id == perk.Id ? perk : p).ToList(), before.Constants);
        var script = after.MigrationScript(before)!;
        Assert.Contains("Items.NewThing", script);
        Assert.Contains("Renamed", script);
        Assert.DoesNotContain("INSERT OR REPLACE INTO vehicle", script);
        Assert.DoesNotContain("INSERT OR REPLACE INTO enum_default", script);

        GameDatabase.Load(DbPath());
        Execute(DbPath(), script);
        var applied = GameDatabase.Load(DbPath());
        Assert.Equal("New Thing", applied.Record(1, 1)?.DisplayName);
        Assert.Equal("Renamed", applied.Perks.Single(p => p.Id == perk.Id).DisplayName);
        // Everything else from the older data is still there.
        Assert.Equal(before.Records.Count + 1, applied.Records.Count);
        Assert.Equal(before.Vehicles, applied.Vehicles);
    }

    [Fact]
    public void LocalDataKeepsTheUsersValuesAcrossUpdates()
    {
        Assert.Null(LocalData.Get(LocalData.GameFolder, DbPath()));
        Assert.True(LocalData.Set(LocalData.GameFolder, "/games/cp", DbPath()));
        Assert.True(LocalData.Set(LocalData.GameFolder, "/games/cp2", DbPath()));
        Assert.True(LocalData.Set("theme", "dark", DbPath()));
        Assert.Equal("/games/cp2", LocalData.Get(LocalData.GameFolder, DbPath()));

        // Starting the app (which runs any pending migrations) leaves the values alone.
        GameDatabase.Load(DbPath());
        Assert.Equal("/games/cp2", LocalData.Get(LocalData.GameFolder, DbPath()));
        Assert.Equal("dark", LocalData.Get("theme", DbPath()));
    }

    [Fact]
    public void NoDataMigrationTouchesLocalData()
    {
        var assembly = typeof(GameDatabase).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Database.")))
        {
            using var r = new StreamReader(assembly.GetManifestResourceStream(name)!);
            Assert.DoesNotContain("local_data", r.ReadToEnd());
        }
        var db = GameDatabase.BuiltIn();
        Assert.DoesNotContain("local_data", db.MigrationScript(null)!);
    }

    [Fact]
    public void TheDatabaseVersionInVersionJsonIsTheSaveVersionTheBuiltInDataCovers() =>
        Assert.Equal(GameDatabase.BuiltIn().Info.MaxSaveVersion, RelicWriter.Common.Updates.AppVersion.DatabaseVersion);

    [Fact]
    public void SavesNewerThanTheDataAreFlagged()
    {
        var db = GameDatabase.BuiltIn();
        Assert.False(db.IsOlderThanSave(db.Info.MaxSaveVersion));
        Assert.True(db.IsOlderThanSave(db.Info.MaxSaveVersion + 1));
    }
}
