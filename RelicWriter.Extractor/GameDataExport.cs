using System.Diagnostics;
using System.IO.Compression;
using RelicWriter.Common;
using RelicWriter.Common.Container;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;
using RelicWriter.GameAssets;

/// <summary>
/// Writes the next migration of the game database (see docs/game-database.md) from the game's files plus the seed lists in Seeds/: the data the
/// existing migrations produce is carried forward, so every build covers all earlier game versions; what the game files give overrides it.
/// </summary>
static class GameDataExport
{
    const string Usage = "Usage: RelicWriter.Extractor gamedata --game <game dir> --out <Database dir> [--saves <saves dir>] [--game-version <version>] [--migrations <folder for the migration classes>]";
    const int RelicMilestoneCost = 3, DefaultCost = 1, OfficialMaxPerkPoints = 82;
    static readonly string[] RelicMilestones = ["Espionage_Central_Milestone_1", "Espionage_Left_Milestone_Perk", "Espionage_Right_Milestone_1"];
    static readonly (string Tree, string AttributeData)[] Trees =
    [
        ("Body", "BodyAttributeData"), ("Reflexes", "ReflexesAttributeData"), ("Tech", "TechnicalAbilityAttributeData"),
        ("Intelligence", "IntelligenceAttributeData"), ("Cool", "CoolAttributeData"), (Perks.RelicTree, "EspionageAttributeData"),
    ];

    // The item record types (TweakDB type hashes, found by looking at which types hold named Items.* records and carry in-game text), with the recipe type kept apart.
    static readonly uint[] ItemTypes = [0x1416443d, 0xb926ec11, 0x7fdef930, 0x1fbcf2d7, 0xff8dad86, 0xe1adebf5, 0x72ddf0c6];
    const uint RecipeType = 0x1fbcf2d7;
    const string CategoryPrefix = "ItemCategory.", TypePrefix = "ItemType.", QualityPrefix = "Quality.";
    static readonly string[] Categories = ["Weapon", "Clothing", "Cyberware", "Consumable", "General", "Gadget", "Part"];
    static readonly string[] Qualities = ["Random", "Common", "CommonPlus", "Uncommon", "UncommonPlus", "Rare", "RarePlus", "Epic", "EpicPlus", "Legendary", "LegendaryPlus", "LegendaryPlusPlus"];
    // Item type names guessed from the game's naming (a prefix for the group, then the kind); the game's own list is not in the files, so a type not named here has no type label.
    static readonly string[] ItemTypeNames = ("Wea_AssaultRifle Wea_Handgun Wea_Shotgun Wea_ShotgunDual Wea_SniperRifle Wea_SubmachineGun Wea_LightMachineGun Wea_HeavyMachineGun Wea_PrecisionRifle Wea_Revolver Wea_Rifle Wea_Melee Wea_Hammer Wea_Katana Wea_Knife Wea_Machete Wea_OneHandedClub Wea_TwoHandedClub Wea_Axe Wea_Fists Wea_Chainsword Wea_LongBlade " +
        "Clo_Face Clo_Feet Clo_Head Clo_InnerChest Clo_Legs Clo_OuterChest Clo_Outfit Con_Ammo Con_Edible Con_Inhaler Con_Injector Con_LongLasting Con_Skillbook " +
        "Gen_CraftingMaterial Gen_Jewellery Gen_Junk Gen_Keycard Gen_Misc Gen_MoneyShard Gen_Readable Gen_Tarot Gad_Grenade Prt_FabricEnhancer Prt_Fragment Prt_HandgunMuzzle Prt_LongScope Prt_Mod Prt_Muzzle Prt_PowerMod Prt_Program Prt_ShortScope " +
        "Cyb_Ability Cyb_Launcher Cyb_MantisBlades Cyb_NanoWires Cyb_StrongArms").Split(' ');

    /// <summary>
    /// The items a player can hold: the game's records of the item types that have in-game text and an item type link, named by that text. Category, type and quality
    /// are the names the records' links point at (found by hashing the known names, since TweakDB stores only hashes). The shape of the save node comes from the items
    /// in the given saves: the shape of the item itself where one was seen, else the most common one for its record type and item type. Items of older runs are kept.
    /// </summary>
    static Dictionary<(uint, byte), ItemInfo> BuildItems(GameNames game, string[] saveFiles, GameDatabase? previous)
    {
        static (uint, byte) Id(string name) { var id = VehicleId.FromName(name); return (id.Hash, id.Length); }
        var categories = Categories.ToDictionary(c => Id(CategoryPrefix + c), c => c);
        var types = ItemTypeNames.ToDictionary(t => Id(TypePrefix + t), t => Spaced(t[(t.IndexOf('_') + 1)..]));
        var qualities = Qualities.ToDictionary(q => Id(QualityPrefix + q), q => q.Replace("PlusPlus", "++").Replace("Plus", "+"));

        var seen = new Dictionary<(uint, byte), ItemShape>();
        foreach (var f in saveFiles)
            if (Inventory.Read(SaveFile.Read(File.ReadAllBytes(f))) is { } inv)
                foreach (var item in inv.SubInventories.SelectMany(i => i.Items)) seen[(item.RecordHash, item.RecordLength)] = item.Shape;

        var rows = new List<(uint Hash, byte Length, uint RecordType, bool InBase, string Name, string Category, (uint, byte) Type, string? Quality)>();
        foreach (var (hash, length, recordType, inBase) in game.Records().Where(r => ItemTypes.Contains(r.Type)))
        {
            // The game marks retired items "!OBSOLETE".
            if (game.Name(hash, length) is not { Length: > 0 } name || name.StartsWith('!') || game.LinkOf(hash, length, ".itemType") is not { } type) continue;
            var category = recordType == RecipeType ? "Recipe" : game.LinkOf(hash, length, ".itemCategory") is { } c && categories.TryGetValue(c, out var label) ? label : "Other";
            rows.Add((hash, length, recordType, inBase, name, category, type, game.LinkOf(hash, length, ".quality") is { } q && qualities.TryGetValue(q, out var ql) ? ql : null));
        }

        // The shape an item without a save of its own most likely has: the most common among the seen items of its record type and item type, else of its record type.
        var observed = rows.Where(r => seen.ContainsKey((r.Hash, r.Length))).ToList();
        ItemShape? Common(IEnumerable<ItemShape> shapes) => shapes.GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => (ItemShape?)g.Key).FirstOrDefault();
        var items = new Dictionary<(uint, byte), ItemInfo>(previous?.Items ?? new Dictionary<(uint, byte), ItemInfo>());
        foreach (var r in rows)
        {
            var shape = seen.TryGetValue((r.Hash, r.Length), out var own) ? own
                : Common(observed.Where(o => o.RecordType == r.RecordType && o.Type == r.Type).Select(o => seen[(o.Hash, o.Length)]))
                ?? Common(observed.Where(o => o.RecordType == r.RecordType).Select(o => seen[(o.Hash, o.Length)])) ?? ItemShape.Other;
            items[(r.Hash, r.Length)] = new ItemInfo(r.Hash, r.Length, r.Name, r.Category, types.GetValueOrDefault(r.Type), r.Quality, shape, !r.InBase, game.DescriptionOf(r.Hash, r.Length));
        }
        return items;
    }

    // "OuterChest" -> "Outer chest"
    static string Spaced(string name)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    public static int Run(Dictionary<string, string> opt)
    {
        if (!opt.TryGetValue("--game", out var gameDir) || !opt.TryGetValue("--out", out var outDir)) { Console.Error.WriteLine(Usage); return 2; }
        var migrationsDir = opt.GetValueOrDefault("--migrations") ?? Path.Combine(outDir, "..", "RelicWriter.Common", "GameData", "Storage", "Migrations");
        if (GameNames.Open(gameDir) is not { } game) { Console.Error.WriteLine("Could not read the game files."); return 1; }
        // The previous data is what the migrations compiled into the Extractor produce (rebuild after every run), so an update only adds a migration and never drops history.
        var existing = Directory.Exists(outDir) ? Directory.GetFiles(outDir, "*.sql").Length : 0;
        if (existing != GameDatabase.ScriptCount) { Console.Error.WriteLine($"{existing} migration scripts in {outDir} but the Extractor was built with {GameDatabase.ScriptCount}: rebuild it first."); return 2; }
        var previous = existing > 0 ? GameDatabase.BuiltIn() : null;

        var enums = new Dictionary<string, string>(previous?.Enums ?? new Dictionary<string, string>());
        foreach (var line in Seed("enums.txt")) { var p = line.Split((char)9); enums[p[0]] = p[1]; }

        // Record names: the seed list (names from CP2077SaveEditor / CyberCAT) plus the vehicles. The game's own name wins, then a parent record's, then the seed's.
        var records = new Dictionary<(uint, byte), RecordEntry>(previous?.Records ?? new Dictionary<(uint, byte), RecordEntry>());
        var vehicleNames = Seed("vehicles.txt");
        foreach (var line in Seed("record-names.txt.gz").Concat(vehicleNames))
        {
            var p = line.Split((char)9);
            var id = VehicleId.FromName(p[0]);
            var name = game.Name(id.Hash, id.Length) ?? Parents(p[0]).Select(parent => { var pid = VehicleId.FromName(parent); return game.Name(pid.Hash, pid.Length); }).FirstOrDefault(n => n is not null)
                ?? (p.Length > 1 ? p[1] : null) ?? records.GetValueOrDefault((id.Hash, id.Length))?.DisplayName;
            records[(id.Hash, id.Length)] = new RecordEntry(p[0], name);
        }

        var oldVehicles = (previous?.Vehicles ?? []).ToDictionary(v => v.Record);
        var vehicles = vehicleNames.Select(v =>
        {
            var id = VehicleId.FromName(v);
            return game.VehicleIconRef(id.Hash, id.Length) is { } icon ? new VehicleEntry(v, icon.Atlas, icon.Part) : oldVehicles.GetValueOrDefault(v) ?? new VehicleEntry(v, null, null);
        }).ToList();
        vehicles.AddRange((previous?.Vehicles ?? []).Where(v => !vehicleNames.Contains(v.Record)));
        foreach (var v in vehicles.Where(v => v.Atlas is null)) Console.Error.WriteLine("no icon: " + v.Record);

        var perkIds = Seed("new-perks.txt").Select(line => line.Split((char)9)).ToList();
        var perks = perkIds.Select(p => new PerkInfo(p[0], p[0][..p[0].IndexOf('_')], int.Parse(p[1]),
            RelicMilestones.Contains(p[0]) ? RelicMilestoneCost : DefaultCost, game.PerkName(p[0]) ?? previous?.Perks.FirstOrDefault(o => o.Id == p[0])?.DisplayName,
            string.Join(' ', game.PerkRequirements(p[0], perkIds.Select(q => q[0])) ?? previous?.Perks.FirstOrDefault(o => o.Id == p[0])?.RequiredPerks ?? []),
            game.PerkAttributePoints(p[0]) ?? previous?.Perks.FirstOrDefault(o => o.Id == p[0])?.AttributePoints ?? 0)).ToList();
        perks.AddRange((previous?.Perks ?? []).Where(o => perks.All(p => p.Id != o.Id)));
        foreach (var p in perks.Where(p => p.DisplayName is null)) Console.Error.WriteLine("no perk name: " + p.Id);

        var constants = new Dictionary<string, int>(previous?.Constants ?? new Dictionary<string, int>()) { [GameDatabase.OfficialMaxPerkPoints] = OfficialMaxPerkPoints };

        var saveFiles = opt.TryGetValue("--saves", out var savesDir) ? Directory.GetDirectories(savesDir).Select(d => Path.Combine(d, "sav.dat")).Where(File.Exists).ToArray() : [];
        var maxSave = saveFiles.Select(f => SaveFile.Read(File.ReadAllBytes(f)).Header.SaveVersion).Append(previous?.Info.MaxSaveVersion ?? 0).Max();
        if (maxSave == 0) { Console.Error.WriteLine("The newest save version is unknown: pass --saves <saves dir> (or --previous)."); return 2; }

        // The version resource of a Windows exe can only be read on Windows; elsewhere pass it.
        var gameVersion = opt.GetValueOrDefault("--game-version") ?? FileVersionInfo.GetVersionInfo(Path.Combine(gameDir, "bin", "x64", "Cyberpunk2077.exe")).ProductVersion
            ?? throw new InvalidDataException("The exe has no readable product version (not on Windows?): pass --game-version <version>.");
        var info = new DatabaseInfo((previous?.Info.DataVersion ?? 0) + 1, gameVersion, maxSave, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        var tree = Trees.ToDictionary(t => t.Tree, t => t.AttributeData);
        var items = BuildItems(game, saveFiles, previous);
        var db = new GameDatabase(info, enums, records, vehicles, tree, perks, constants, items);
        if (db.MigrationScript(previous) is not { } script) { Console.WriteLine($"No change from data v{previous!.Info.DataVersion}; no migration written."); return 0; }
        Directory.CreateDirectory(outDir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var migrationId = $"{stamp}_Game_{gameVersion}";
        File.WriteAllText(Path.Combine(outDir, migrationId + ".sql"), script);
        // The class EF finds the migration by; it runs the script of the same id. Rebuild to pick both up.
        File.WriteAllText(Path.Combine(migrationsDir, $"M{stamp}.cs"), $$"""
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace RelicWriter.Common.GameData.Storage.Migrations;

            [DbContext(typeof(GameDbContext)), Migration("{{migrationId}}")]
            internal sealed class M{{stamp}} : SqlScriptMigration;
            """);
        // version.json's "database" is the save version the data was built for.
        var versionFile = Path.Combine(outDir, "..", "version.json");
        if (File.Exists(versionFile))
            File.WriteAllText(versionFile, System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(versionFile), "(\"database\"\\s*:\\s*)\\d+", "${1}" + maxSave));
        Console.WriteLine($"{migrationId}: save versions up to {maxSave}; now {records.Count} records, {items.Count} items, {vehicles.Count} vehicles, {perks.Count} perks, {enums.Count} enums. Rebuild to include it.");
        return 0;
    }

    // "Items.Helmet_10_rich_01" -> "Items.Helmet_10_rich" -> "Items.Helmet_10": variants take their name from a parent record.
    static IEnumerable<string> Parents(string record)
    {
        for (var name = record; name.IndexOf('_', name.IndexOf('.') + 1) >= 0;)
            yield return name = name[..name.LastIndexOf('_')];
    }

    static string[] Seed(string file)
    {
        using var s = typeof(GameDataExport).Assembly.GetManifestResourceStream("RelicWriter.Extractor.Seeds." + file)!;
        using var r = new StreamReader(file.EndsWith(".gz") ? new GZipStream(s, CompressionMode.Decompress) : s);
        return r.ReadToEnd().Split((char)10, StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd((char)13)).Where(l => l.Length > 0).ToArray();
    }
}
