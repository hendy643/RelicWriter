using RelicWriter.Common.Container;
using RelicWriter.Common.Formats;

namespace RelicWriter.Common.Model;

public sealed class ProficiencyEntry
{
    internal BlobStruct Source { get; init; } = null!;
    public required string Type { get; init; }
    public int Level { get; set; }
}

public sealed class AttributeEntry
{
    internal BlobStruct Source { get; init; } = null!;

    /// <summary>The stat name the game stores (Strength, Reflexes, TechnicalAbility, Intelligence, Cool).</summary>
    public required string Stat { get; init; }
    public int Value { get; set; }
}

public sealed class DevelopmentPointsEntry
{
    internal BlobStruct Source { get; init; } = null!;
    public required string Type { get; init; }
    public int Unspent { get; set; }
}

/// <summary>
/// The player's character sheet: level, street cred, attributes, skills and unspent points, read from the PlayerDevelopmentData object
/// in ScriptableSystemsContainer. Fields the game leaves out because they hold the default are read as 0.
/// </summary>
public sealed class PlayerDevelopment
{
    /// <summary>The player is the entity whose id hash is 1.</summary>
    const ulong PlayerEntityHash = 1;

    public static readonly string[] AttributeStats = ["Strength", "Reflexes", "TechnicalAbility", "Intelligence", "Cool"];

    readonly SystemBlob blob;
    readonly BlobStruct data;
    readonly List<BlobStruct> proficiencyStructs, attributeStructs, pointStructs, attributeDataStructs;
    readonly HashSet<BlobStruct> changedTrees = [];

    PlayerDevelopment(SystemBlob blob, BlobStruct data)
    {
        this.blob = blob;
        this.data = data;
        proficiencyStructs = data.GetStructs("proficiencies");
        attributeStructs = data.GetStructs("attributes");
        pointStructs = data.GetStructs("devPoints");
        attributeDataStructs = data.GetStructs("attributesData");
        Proficiencies = proficiencyStructs.Select(s => new ProficiencyEntry { Source = s, Type = s.GetEnum("type") ?? BlobTypes.EnumDefault("gamedataProficiencyType")!, Level = s.GetInt("currentLevel") ?? 0 }).ToList();
        Attributes = attributeStructs.Select(s => new AttributeEntry { Source = s, Stat = s.GetEnum("attributeName") ?? BlobTypes.EnumDefault("gamedataStatType")!, Value = s.GetInt("value") ?? 0 })
            .Where(a => AttributeStats.Contains(a.Stat)).OrderBy(a => Array.IndexOf(AttributeStats, a.Stat)).ToList();
        Points = pointStructs.Select(s => new DevelopmentPointsEntry { Source = s, Type = s.GetEnum("type") ?? BlobTypes.EnumDefault("gamedataDevelopmentPointType")!, Unspent = s.GetInt("unspent") ?? 0 }).ToList();
        LifePath = data.GetEnum("lifePath") ?? BlobTypes.EnumDefault("gamedataLifePath") ?? "";
    }

    public string LifePath { get; }

    /// <summary>True when the save has the Relic perk data (Phantom Liberty saves do; older ones do not).</summary>
    public bool HasTree(string tree) => Tree(tree) is not null;

    public bool HasRelic => Tree(Perks.RelicTree) is not null;

    // A value equal to the default is left out of the save, so a missing type is the enum's first value.
    BlobStruct? Tree(string tree) => attributeDataStructs.FirstOrDefault(s => (s.GetEnum("type") ?? BlobTypes.EnumDefault("gamedataAttributeDataType")) == Perks.TreeData[tree]);

    static string PerkName(BlobStruct s) => s.GetEnum("type") ?? BlobTypes.EnumDefault("gamedataNewPerkType")!;

    // Cached per tree: the perk structs are rewritten into the tree only when something changed.
    readonly Dictionary<string, List<BlobStruct>> treePerks = [];

    List<BlobStruct>? PerkStructs(string tree)
    {
        if (Tree(tree) is not { } t) return null;
        if (!treePerks.TryGetValue(tree, out var list)) treePerks[tree] = list = t.GetStructs("unlockedPerks");
        return list;
    }

    /// <summary>The bought level of a perk, 0 when the character doesn't have it.</summary>
    public int PerkLevel(PerkInfo perk) => PerkStructs(perk.Tree)?.FirstOrDefault(s => PerkName(s) == perk.Id)?.GetInt("currLevel") ?? 0;

    /// <summary>Sets a perk's level, clamped to its maximum; 0 takes the perk away. Does nothing when the save has no data for the perk's tree.</summary>
    public void SetPerkLevel(PerkInfo perk, int level)
    {
        if (PerkStructs(perk.Tree) is not { } list) return;
        level = Math.Clamp(level, 0, perk.MaxLevel);
        if (level == PerkLevel(perk)) return;
        list.RemoveAll(s => PerkName(s) == perk.Id);
        if (level > 0)
        {
            var s = Tree(perk.Tree)!.NewStruct();
            s.SetEnum("type", "gamedataNewPerkType", perk.Id);
            s.SetInt("currLevel", level);
            list.Add(s);
        }
        changedTrees.Add(Tree(perk.Tree)!);
    }

    // The game keeps an entry (level 0 if need be) for every perk it has unlocked: all the perks it requires at their top level and the attribute at its
    // tier's value. It buys only unlocked perks, and takes the entries of dependent perks away when a perk is sold.
    void SyncUnlocked(string tree)
    {
        var list = PerkStructs(tree)!;
        var points = Attributes.FirstOrDefault(a => a.Stat == Perks.AttributeStat(tree))?.Value ?? 0;
        var all = Perks.All.ToDictionary(p => p.Id);
        foreach (var perk in Perks.All.Where(p => p.Tree == tree))
        {
            var unlocked = points >= perk.AttributePoints && perk.RequiredPerks.All(r => all.TryGetValue(r, out var q) && PerkLevel(q) == q.MaxLevel);
            var has = list.Any(s => PerkName(s) == perk.Id);
            if (unlocked && !has)
            {
                var s = Tree(tree)!.NewStruct();
                s.SetEnum("type", "gamedataNewPerkType", perk.Id);
                list.Add(s);
            }
            else if (!unlocked && has && PerkLevel(perk) == 0) list.RemoveAll(s => PerkName(s) == perk.Id);
        }
    }

    /// <summary>Every perk entry in the save: the attribute tree it belongs to, the perk and its level.</summary>
    public IEnumerable<(string Tree, string Perk, int Level)> PerkEntries =>
        attributeDataStructs.SelectMany(a => a.GetStructs("unlockedPerks").Select(p => (a.GetEnum("type") ?? BlobTypes.EnumDefault("gamedataAttributeDataType")!, PerkName(p), p.GetInt("currLevel") ?? 0)));

    public List<ProficiencyEntry> Proficiencies { get; }

    public List<AttributeEntry> Attributes { get; }

    public List<DevelopmentPointsEntry> Points { get; }

    public ProficiencyEntry? Level => Proficiencies.FirstOrDefault(p => p.Type == "Level");

    public ProficiencyEntry? StreetCred => Proficiencies.FirstOrDefault(p => p.Type == "StreetCred");

    /// <summary>Skill proficiencies: everything that is not the character level or street cred.</summary>
    public IEnumerable<ProficiencyEntry> Skills => Proficiencies.Where(p => p.Type is not ("Level" or "StreetCred"));

    /// <summary>Reads the player's development data, or null when the save has none (the node is missing or has no PlayerDevelopmentData).</summary>
    public static PlayerDevelopment? Read(SaveFile file)
    {
        var node = file.Walk().FirstOrDefault(n => n.Name == "ScriptableSystemsContainer");
        if (node is null) return null;
        var blob = SystemBlob.Parse(node.Data);
        var candidates = blob.OfClass("PlayerDevelopmentData").Where(o => o.Body.Find("attributes") is not null).ToList();
        // Older saves hold one per entity; the player's has owner id 1.
        var player = candidates.FirstOrDefault(o => o.Body.GetStruct("ownerID")?.GetUInt64("hash") == PlayerEntityHash) ?? candidates.FirstOrDefault();
        return player is null ? null : new PlayerDevelopment(blob, player.Body);
    }

    /// <summary>Writes the edited values back into the save's ScriptableSystemsContainer node.</summary>
    public void ApplyTo(SaveFile file)
    {
        // Only changed values are written, so an unedited save stays byte-identical (the game leaves default values out).
        foreach (var p in Proficiencies) SetIfChanged(p.Source, "currentLevel", p.Level);
        foreach (var a in Attributes) SetIfChanged(a.Source, "value", a.Value);
        foreach (var p in Points) SetIfChanged(p.Source, "unspent", p.Unspent);
        data.SetStructs("proficiencies", proficiencyStructs);
        data.SetStructs("attributes", attributeStructs);
        data.SetStructs("devPoints", pointStructs);
        // Only trees whose perks or attribute were edited are brought in step, so an unedited save stays byte-identical.
        foreach (var tree in Perks.TreeData.Keys.Where(k => Tree(k) is not null))
            if (changedTrees.Contains(Tree(tree)!) || Attributes.Any(a => a.Stat == Perks.AttributeStat(tree) && a.Value != (a.Source.GetInt("value") ?? 0)))
            {
                SyncUnlocked(tree);
                changedTrees.Add(Tree(tree)!);
            }
        if (changedTrees.Count > 0)
        {
            foreach (var tree in Perks.TreeData.Keys.Where(k => Tree(k) is { } t && changedTrees.Contains(t))) Tree(tree)!.SetStructs("unlockedPerks", treePerks[tree], "array:SNewPerk");
            data.SetStructs("attributesData", attributeDataStructs);
            changedTrees.Clear();
        }
        file.Walk().First(n => n.Name == "ScriptableSystemsContainer").Data = blob.Write();
    }

    static void SetIfChanged(BlobStruct s, string field, int value)
    {
        if (value != (s.GetInt(field) ?? 0)) s.SetInt(field, value);
    }
}
