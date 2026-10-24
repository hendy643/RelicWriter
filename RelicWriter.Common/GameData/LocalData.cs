using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RelicWriter.Common.GameData.Storage;

namespace RelicWriter.Common.GameData;

/// <summary>
/// The user's own values (the game folder, settings) in the <c>local_data</c> table of the game database file. The table is created by the schema
/// migration and no data migration ever writes to it, so updating the game data never changes what the user set.
/// </summary>
public static class LocalData
{
    public const string GameFolder = "game_folder";

    /// <summary>The value stored under <paramref name="key"/>, or null when there is none or the file can't be read.</summary>
    public static string? Get(string key, string? path = null)
    {
        path ??= GameDatabase.UserPath;
        if (!File.Exists(path)) return null;
        try
        {
            using var db = Open(path);
            return db.LocalData.AsNoTracking().FirstOrDefault(r => r.Key == key)?.Value;
        }
        catch (Exception e) when (e is DbException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    /// <summary>Stores a value, bringing the file up to date first if it is new. False when the file can't be written.</summary>
    public static bool Set(string key, string value, string? path = null)
    {
        path ??= GameDatabase.UserPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var db = Open(path);
            db.Database.Migrate();
            if (db.LocalData.Find(key) is { } row) row.Value = value;
            else db.LocalData.Add(new LocalDataRow { Key = key, Value = value });
            db.SaveChanges();
            return true;
        }
        catch (Exception e) when (e is DbException or InvalidOperationException or IOException or UnauthorizedAccessException or DbUpdateException)
        {
            return false;
        }
    }

    static GameDbContext Open(string path) =>
        new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()).Options);
}
