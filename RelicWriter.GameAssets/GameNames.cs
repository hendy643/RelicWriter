using RelicWriter.Common;
namespace RelicWriter.GameAssets;

/// <summary>
/// In-game names and pictures for records, read from the user's own game files at run time (nothing from the game is bundled):
/// the record's displayName flat in the TweakDB gives a localisation key, and the English on-screen text gives the name; a vehicle's
/// icon flat gives a texture atlas part, which is cut out of the atlas texture.
/// </summary>
public sealed class GameNames
{
    const string LocKeyPrefix = "LocKey#";
    // The base game and Phantom Liberty each keep their English on-screen text in their own archive, under their own path.
    const string OnScreensPath = @"base\localization\en-us\onscreens\onscreens.json", Ep1OnScreensPath = @"ep1\localization\en-us\onscreens\onscreens.json";

    readonly TweakDb db;
    readonly OnScreens screens;

    GameNames(TweakDb db, OnScreens screens)
    {
        this.db = db;
        this.screens = screens;
    }

    /// <summary>The names in use for the app; set once the game folder is known. Null until then, or when the game files can't be read.</summary>
    public static GameNames? Current { get; set; }

    /// <summary>Reads the TweakDB (base game, then Phantom Liberty) and the English text. Null if the folder isn't a readable install.</summary>
    public static GameNames? Open(string gameDir)
    {
        try
        {
            var cache = Path.Combine(gameDir, "r6", "cache");
            var db = TweakDb.Open(new[] { "tweakdb.bin", "tweakdb_ep1.bin" }.Select(f => Path.Combine(cache, f)).Where(File.Exists));
            if (db is null) return null;

            var archives = Path.Combine(gameDir, "archive", "pc");
            var screens = new OnScreens();
            foreach (var (archive, textPath) in new[] { (Path.Combine("content", "lang_en_text.archive"), OnScreensPath), (Path.Combine("ep1", "lang_en_text.archive"), Ep1OnScreensPath) })
            {
                var path = Path.Combine(archives, archive);
                if (File.Exists(path) && new GameArchive(path).Read(GameArchive.PathHash(textPath)) is { } text) screens.Add(text);
            }
            if (screens.Count == 0) return null;

            return new GameNames(db, screens);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The in-game name of a record such as an item or vehicle, or null when the game files have none.</summary>
    public string? Name(uint hash, byte length) => Text(TweakDb.Append(TweakDb.Id(hash, length), ".displayName"));

    /// <summary>The in-game description of a record such as an item (its localizedDescription), or null when the game files have none.</summary>
    public string? DescriptionOf(uint hash, byte length) => Text(TweakDb.Append(TweakDb.Id(hash, length), ".localizedDescription"));

    /// <summary>Every record in the TweakDB with the hash of its type; see <see cref="TweakDb.Records"/>.</summary>
    public IEnumerable<(uint Hash, byte Length, uint Type, bool InBaseGame)> Records() => db.Records();

    /// <summary>The record a link flat of a record points to ("itemType", "itemCategory", ...), as (hash, name length); null when the record has no such flat.</summary>
    public (uint Hash, byte Length)? LinkOf(uint hash, byte length, string field) =>
        db.Link(TweakDb.Append(TweakDb.Id(hash, length), field)) is { } id ? ((uint)id, (byte)(id >> 32)) : null;

    /// <summary>The in-game name of a perk (a gamedataNewPerkType value such as "Body_Central_Perk_1_1"), or null when the game files have none.</summary>
    public string? PerkName(string perk) => PerkText(perk, ".loc_name_key");

    // A perk record's name field holds "LocKey#number" (a few are spelled "Lockey#"): the primary key of an on-screen text.
    string? PerkText(string perk, string field)
    {
        var record = "NewPerks." + perk;
        var key = db.String(TweakDb.Append(TweakDb.Id(Hashing.Crc32(System.Text.Encoding.ASCII.GetBytes(record)), record.Length), field));
        return key is not null && key.StartsWith(LocKeyPrefix, StringComparison.OrdinalIgnoreCase) && ulong.TryParse(key[LocKeyPrefix.Length..], out var primary) ? screens.Text(primary) : null;
    }

    /// <summary>The perks that must be at their top level before this perk can be bought (its requiresPerks), by name; null when the game files don't list the perk.</summary>
    public string[]? PerkRequirements(string perk, IEnumerable<string> allPerks)
    {
        static ulong PerkId(string p) => TweakDb.Id(Hashing.Crc32(System.Text.Encoding.ASCII.GetBytes("NewPerks." + p)), "NewPerks.".Length + p.Length);
        var ids = db.Links(TweakDb.Append(PerkId(perk), ".requiresPerks"));
        if (ids is null) return null;
        var byId = allPerks.ToDictionary(PerkId);
        return ids.Select(id => byId[id]).ToArray();
    }

    /// <summary>The attribute value a perk needs before it can be bought: the requiredAttributePoints of its tier record. Null when the game files don't say.</summary>
    public int? PerkAttributePoints(string perk)
    {
        var record = "NewPerks." + perk;
        var id = TweakDb.Id(Hashing.Crc32(System.Text.Encoding.ASCII.GetBytes(record)), record.Length);
        return db.Link(TweakDb.Append(id, ".tier")) is { } tier ? db.Int(TweakDb.Append(tier, ".requiredAttributePoints")) : null;
    }

    string? Text(ulong flat) => db.LocKey(flat) is { } key ? screens.Text(key) : null;

    /// <summary>Where the game keeps a vehicle's picture: its icon record names a texture atlas (the hash of the atlas file's path) and a part of it. Null if the game has none.</summary>
    public (ulong Atlas, string Part)? VehicleIconRef(uint hash, byte length)
    {
        if (db.Link(TweakDb.Append(TweakDb.Id(hash, length), ".icon")) is not { } icon) return null;
        return db.Resource(TweakDb.Append(icon, ".atlasResourcePath")) is { } atlas && db.Name(TweakDb.Append(icon, ".atlasPartName")) is { } part ? (atlas, part) : null;
    }
}
