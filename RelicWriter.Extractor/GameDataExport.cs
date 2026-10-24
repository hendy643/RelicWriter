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
    const string Usage = "Usage: RelicWriter.Extractor gamedata --game <game dir> --out <Database dir> [--saves <saves dir>] [--migrations <folder for the migration classes>]";
    const int RelicMilestoneCost = 3, DefaultCost = 1, OfficialMaxPerkPoints = 82;
    static readonly string[] RelicMilestones = ["Espionage_Central_Milestone_1", "Espionage_Left_Milestone_Perk", "Espionage_Right_Milestone_1"];
    static readonly (string Tree, string AttributeData)[] Trees =
    [
        ("Body", "BodyAttributeData"), ("Reflexes", "ReflexesAttributeData"), ("Tech", "TechnicalAbilityAttributeData"),
        ("Intelligence", "IntelligenceAttributeData"), ("Cool", "CoolAttributeData"), (Perks.RelicTree, "EspionageAttributeData"),
    ];

    public static int Run(Dictionary<string, string> opt)
    {
        if (!opt.TryGetValue("--game", out var gameDir) || !opt.TryGetValue("--out", out var outDir)) { Console.Error.WriteLine(Usage); return 2; }
        var migrationsDir = opt.GetValueOrDefault("--migrations") ?? Path.Combine(outDir, "..", "RelicWriter.Common", "GameData", "Storage", "Migrations");
        if (GameNames.Open(gameDir) is not { } game) { Console.Error.WriteLine("Could not read the game files (Windows and the game's Oodle DLL are needed)."); return 1; }
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

        var maxSave = opt.TryGetValue("--saves", out var savesDir)
            ? Directory.GetDirectories(savesDir).Select(d => Path.Combine(d, "sav.dat")).Where(File.Exists).Select(f => SaveFile.Read(File.ReadAllBytes(f)).Header.SaveVersion).Append(previous?.Info.MaxSaveVersion ?? 0).Max()
            : previous?.Info.MaxSaveVersion ?? 0;
        if (maxSave == 0) { Console.Error.WriteLine("The newest save version is unknown: pass --saves <saves dir> (or --previous)."); return 2; }

        var gameVersion = FileVersionInfo.GetVersionInfo(Path.Combine(gameDir, "bin", "x64", "Cyberpunk2077.exe")).ProductVersion ?? throw new InvalidDataException("The exe has no product version.");
        var info = new DatabaseInfo((previous?.Info.DataVersion ?? 0) + 1, gameVersion, maxSave, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        var tree = Trees.ToDictionary(t => t.Tree, t => t.AttributeData);
        var db = new GameDatabase(info, enums, records, vehicles, tree, perks, constants);
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
        Console.WriteLine($"{migrationId}: save versions up to {maxSave}; now {records.Count} records, {vehicles.Count} vehicles, {perks.Count} perks, {enums.Count} enums. Rebuild to include it.");
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
