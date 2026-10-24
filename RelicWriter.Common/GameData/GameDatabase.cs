using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RelicWriter.Common.GameData.Storage;
using RelicWriter.Common.Model;

namespace RelicWriter.Common.GameData;

public enum DatabaseProblem { Unreadable, TooNew }

public sealed class GameDatabaseException(DatabaseProblem problem, string message, Exception? inner = null) : Exception(message, inner)
{
    public DatabaseProblem Problem { get; } = problem;
}

/// <param name="DataVersion">How many migrations have run (the first is the schema, made by EF); it rises with every update; it rises with every update.</param>
/// <param name="GameVersion">The game build the data was read from, for example "2.5".</param>
/// <param name="MaxSaveVersion">The newest save version the data covers; saves from a later game build may use records it lacks.</param>
public sealed record DatabaseInfo(int DataVersion, string GameVersion, uint MaxSaveVersion, string Created)
{
    public string Describe => $"GAME DATA {GameVersion} (v{DataVersion})";
}

public sealed record RecordEntry(string Name, string? DisplayName);

/// <summary>A vehicle and where the game keeps its picture: the hash of the texture atlas file's path and the part of the atlas. The picture itself is read from the player's game files.</summary>
public sealed record VehicleEntry(string Record, ulong? Atlas, string? AtlasPart);

/// <summary>
/// Everything the app knows about the game, held in memory: enum defaults for the save blobs, record names (items, vehicles), the vehicle list
/// with where their pictures are in the game files, and the perk trees. Built from the game's own files by <c>RelicWriter.Extractor gamedata</c>; the app never reads the game files
/// for this. The data lives in a SQLite file in the user's local data folder, managed with Entity Framework Core: the schema is an EF migration and
/// each update is a data migration, a numbered SQL script in the Database folder (compiled into the app) run once by a one-line migration class.
/// An update is a new migration, not a new database. See docs/game-database.md.
/// </summary>
public sealed class GameDatabase
{
    public const string FileName = "gamedata.db";
    public const string OfficialMaxPerkPoints = "official_max_perk_points";

    const string MetaGameVersion = "game_version", MetaMaxSaveVersion = "max_save_version", MetaCreated = "created_utc";
    const string ScriptPrefix = "Database.", ScriptSuffix = ".sql";

    static GameDatabase? current;

    /// <summary>The data in use: the database file in the user's local data folder, migrated to the newest migration this build has. Tests and tools may set it.</summary>
    public static GameDatabase Current
    {
        get => current ??= Load(UserPath);
        set => current = value;
    }

    /// <summary>The database file in the user's local data folder. It is created on first start and kept; updates run migrations on it.</summary>
    public static string UserPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RelicWriter", FileName);

    /// <summary>How many data migration scripts are compiled into the app.</summary>
    internal static int ScriptCount => typeof(GameDatabase).Assembly.GetManifestResourceNames().Count(n => n.StartsWith(ScriptPrefix, StringComparison.Ordinal));

    /// <summary>The text of a data migration's SQL script, which is compiled into the app.</summary>
    internal static string Script(string migrationId)
    {
        using var r = new StreamReader(typeof(GameDatabase).Assembly.GetManifestResourceStream(ScriptPrefix + migrationId + ScriptSuffix)
            ?? throw new InvalidOperationException($"No script for migration {migrationId}."));
        return r.ReadToEnd();
    }

    /// <summary>The built-in data on its own, migrated into an in-memory database; the data folder is not touched.</summary>
    public static GameDatabase BuiltIn()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options);
        return Migrate(db);
    }

    internal GameDatabase(DatabaseInfo info, IReadOnlyDictionary<string, string> enums, IReadOnlyDictionary<(uint Hash, byte Length), RecordEntry> records,
        IReadOnlyList<VehicleEntry> vehicles, IReadOnlyDictionary<string, string> perkTrees, IReadOnlyList<PerkInfo> perks, IReadOnlyDictionary<string, int> constants)
    {
        Info = info;
        Enums = enums;
        Records = records;
        Vehicles = vehicles;
        PerkTrees = perkTrees;
        Perks = perks;
        Constants = constants;
    }

    public DatabaseInfo Info { get; }

    internal IReadOnlyDictionary<string, string> Enums { get; }

    internal IReadOnlyDictionary<(uint Hash, byte Length), RecordEntry> Records { get; }

    public IReadOnlyList<VehicleEntry> Vehicles { get; }

    /// <summary>Tree name to the name the save gives its attribute data, in the game's order.</summary>
    public IReadOnlyDictionary<string, string> PerkTrees { get; }

    public IReadOnlyList<PerkInfo> Perks { get; }

    internal IReadOnlyDictionary<string, int> Constants { get; }

    /// <summary>The value an enum field has when the save leaves it out, or null for a name that is not an enum.</summary>
    public string? EnumDefault(string type) => Enums.GetValueOrDefault(type);

    public RecordEntry? Record(uint hash, byte length) => Records.GetValueOrDefault((hash, length));

    /// <summary>True when a save is newer than the data: its records may be missing here, so names fall back to readable forms.</summary>
    public bool IsOlderThanSave(uint saveVersion) => saveVersion > Info.MaxSaveVersion;

    /// <summary>
    /// Opens the database file (created if missing), runs the migrations it has not run yet and reads it. If the file can't be used (damaged, not
    /// writable, or already migrated by a newer app) the built-in data is used instead, so the app still starts.
    /// </summary>
    internal static GameDatabase Load(string path)
    {
        try
        {
            return Migrate(path);
        }
        catch (Exception e) when (e is GameDatabaseException or DbException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return BuiltIn();
        }
    }

    /// <summary>Runs the pending migrations on the file, then reads it.</summary>
    internal static GameDatabase Migrate(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()).Options);
        return Migrate(db);
    }

    /// <summary>
    /// Runs the migrations a database has not run yet, each in its own transaction, then reads it into memory so the file can be replaced while the app runs.
    /// A database that has run a migration this app does not have is newer than the app and is refused (<see cref="DatabaseProblem.TooNew"/>); one that
    /// lacks migrations is simply brought up to date, so older databases always work.
    /// </summary>
    static GameDatabase Migrate(GameDbContext db)
    {
        var known = db.Database.GetMigrations().ToHashSet();
        if (db.Database.GetAppliedMigrations().FirstOrDefault(id => !known.Contains(id)) is { } unknown)
            throw new GameDatabaseException(DatabaseProblem.TooNew, $"Migration {unknown} is not known to this app; update the app.");
        db.Database.Migrate();
        try
        {
            return Read(db);
        }
        catch (Exception e) when (e is KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new GameDatabaseException(DatabaseProblem.Unreadable, "Not a game database: " + e.Message, e);
        }
    }

    static GameDatabase Read(GameDbContext db)
    {
        var meta = db.Meta.AsNoTracking().ToDictionary(m => m.Key, m => m.Value);
        var info = new DatabaseInfo(db.Database.GetAppliedMigrations().Count(), meta[MetaGameVersion], uint.Parse(meta[MetaMaxSaveVersion]), meta[MetaCreated]);
        return new GameDatabase(info,
            db.EnumDefaults.AsNoTracking().ToDictionary(e => e.Type, e => e.Value),
            db.Records.AsNoTracking().ToDictionary(r => ((uint)r.Hash, (byte)r.Length), r => new RecordEntry(r.Name, r.DisplayName)),
            db.Vehicles.AsNoTracking().OrderBy(v => v.Sort).AsEnumerable().Select(v => new VehicleEntry(v.Record, v.Atlas is { } atlas ? unchecked((ulong)atlas) : null, v.AtlasPart)).ToList(),
            db.PerkTrees.AsNoTracking().OrderBy(t => t.Sort).ToDictionary(t => t.Name, t => t.AttributeData),
            db.Perks.AsNoTracking().OrderBy(p => p.Sort).AsEnumerable().Select(p => new PerkInfo(p.Id, p.Tree, p.MaxLevel, p.CostPerLevel, p.DisplayName)).ToList(),
            db.Constants.AsNoTracking().ToDictionary(c => c.Name, c => c.Value));
    }

    /// <summary>
    /// The SQL that takes a database holding <paramref name="previous"/> to this data: one INSERT OR REPLACE for every row that is new or changed (rows are
    /// never deleted, so records of older game versions stay). Everything when <paramref name="previous"/> is null. Null when nothing changed.
    /// </summary>
    internal string? MigrationScript(GameDatabase? previous)
    {
        static string Q(string? text) => text is null ? "NULL" : "'" + text.Replace("'", "''") + "'";
        var s = new List<string>();
        s.AddRange(Enums.Where(e => previous?.Enums.GetValueOrDefault(e.Key) != e.Value).OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => $"INSERT OR REPLACE INTO enum_default (type, value) VALUES ({Q(e.Key)}, {Q(e.Value)});"));
        s.AddRange(Records.Where(r => previous?.Records.GetValueOrDefault(r.Key) != r.Value).OrderBy(r => r.Value.Name, StringComparer.Ordinal)
            .Select(r => $"INSERT OR REPLACE INTO record (hash, length, name, display_name) VALUES ({r.Key.Hash}, {r.Key.Length}, {Q(r.Value.Name)}, {Q(r.Value.DisplayName)});"));
        var oldVehicles = previous?.Vehicles.Select((v, i) => (v, i)).ToDictionary(p => p.v.Record) ?? [];
        s.AddRange(Vehicles.Select((v, i) => (v, i)).Where(p => !oldVehicles.TryGetValue(p.v.Record, out var o) || o.i != p.i || o.v != p.v)
            .Select(p => $"INSERT OR REPLACE INTO vehicle (record, sort, atlas, atlas_part) VALUES ({Q(p.v.Record)}, {p.i}, {(p.v.Atlas is { } atlas ? unchecked((long)atlas).ToString() : "NULL")}, {Q(p.v.AtlasPart)});"));
        var oldTrees = previous?.PerkTrees.Select((t, i) => (t, i)).ToDictionary(p => p.t.Key) ?? [];
        s.AddRange(PerkTrees.Select((t, i) => (t, i)).Where(p => !oldTrees.TryGetValue(p.t.Key, out var o) || o.i != p.i || o.t.Value != p.t.Value)
            .Select(p => $"INSERT OR REPLACE INTO perk_tree (name, sort, attribute_data) VALUES ({Q(p.t.Key)}, {p.i}, {Q(p.t.Value)});"));
        var oldPerks = previous?.Perks.Select((p, i) => (p, i)).ToDictionary(p => p.p.Id) ?? [];
        s.AddRange(Perks.Select((p, i) => (p, i)).Where(x => !oldPerks.TryGetValue(x.p.Id, out var o) || o.i != x.i || o.p != x.p)
            .Select(x => $"INSERT OR REPLACE INTO perk (id, tree, sort, max_level, cost_per_level, display_name) VALUES ({Q(x.p.Id)}, {Q(x.p.Tree)}, {x.i}, {x.p.MaxLevel}, {x.p.CostPerLevel}, {Q(x.p.DisplayName)});"));
        s.AddRange(Constants.Where(k => previous?.Constants.GetValueOrDefault(k.Key, int.MinValue) != k.Value).OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => $"INSERT OR REPLACE INTO constant (name, value) VALUES ({Q(k.Key)}, {k.Value});"));
        if (s.Count == 0 && previous is not null && previous.Info.GameVersion == Info.GameVersion && previous.Info.MaxSaveVersion == Info.MaxSaveVersion) return null;
        s.Add($"INSERT OR REPLACE INTO meta (key, value) VALUES ('{MetaGameVersion}', {Q(Info.GameVersion)});");
        s.Add($"INSERT OR REPLACE INTO meta (key, value) VALUES ('{MetaMaxSaveVersion}', {Q(Info.MaxSaveVersion.ToString())});");
        s.Add($"INSERT OR REPLACE INTO meta (key, value) VALUES ('{MetaCreated}', {Q(Info.Created)});");
        return string.Join('\n', s) + '\n';
    }
}
