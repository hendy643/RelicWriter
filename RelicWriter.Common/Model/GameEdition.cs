namespace RelicWriter.Common.Model;

/// <summary>A game patch a save can belong to. <paramref name="SaveVersion"/> is the save version the game wrote at that patch, or null when no save of it has been seen.</summary>
public sealed record GamePatch(string Name, uint GameVersion, uint? SaveVersion);

/// <summary>
/// What the game allows at a given patch, and whether the Phantom Liberty expansion is part of it. The save does not record the expansion
/// (the Relic entries are in every 2.0 and later save), so it is a separate setting; <see cref="LooksLikePhantomLiberty"/> guesses it for a loaded save.
/// Sources: CD PROJEKT RED's Update 2.0 notes (skills merged into five new ones with a maximum of 60, the perk trees rebuilt) and the
/// expansion's announced split (level cap 60 and the Relic tree only with Phantom Liberty); before 2.0 the level cap was 50 and skills stopped at 20,
/// which the saves of 1.21 to 1.63 agree with. See docs/game-versions.md.
/// </summary>
public sealed record GameEdition(uint GameVersion, bool PhantomLiberty)
{
    /// <summary>The first game version with the new skills, the rebuilt perk trees and (with the expansion) the Relic tree.</summary>
    public const uint SkillRework = 2000;

    public const int MaxStreetCred = 50, MaxAttribute = 20, MinAttribute = 3, MaxLegacySkill = 20, MaxSkill = 60, MaxBaseLevel = 50, MaxExpansionLevel = 60;

    /// <summary>The 12 skills of 1.x. 2.0 and later saves still carry them, but the game no longer uses them.</summary>
    static readonly HashSet<string> LegacySkills = ["Assault", "Athletics", "Brawling", "ColdBlood", "CombatHacking", "Crafting", "Demolition", "Engineering", "Gunslinger", "Hacking", "Kenjutsu", "Stealth"];

    /// <summary>The Phantom Liberty skill, in the proficiencies as "Espionage".</summary>
    const string RelicSkill = "Espionage";

    /// <summary>Patches in release order. Save versions come from real saves; the others are unknown (null).</summary>
    public static readonly IReadOnlyList<GamePatch> Patches =
    [
        new("1.1", 1100, null), new("1.2", 1200, null), new("1.21", 1210, 197), new("1.22", 1220, 197), new("1.23", 1230, null), new("1.3", 1300, null),
        new("1.4", 1310, null), new("1.5", 1500, 220), new("1.6", 1600, 224), new("1.61", 1610, 224), new("1.62", 1620, null), new("1.63", 1630, 224),
        new("2.0", 2000, 254), new("2.01", 2010, null), new("2.02", 2020, null), new("2.1", 2100, 261), new("2.11", 2110, null), new("2.12", 2120, 263),
        new("2.13", 2137, null), new("2.2", 2200, null), new("2.21", 2210, 265), new("2.3", 2300, 269), new("2.31", 2310, 269), new("2.5", 2500, 277),
    ];

    public bool HasNewSkills => GameVersion >= SkillRework;

    /// <summary>The perk trees the editor knows are the 2.x ones; 1.x saves use another system.</summary>
    public bool HasPerkTrees => GameVersion >= SkillRework;

    public bool HasPhantomLiberty => PhantomLiberty && GameVersion >= SkillRework;

    public int MaxLevel => HasPhantomLiberty ? MaxExpansionLevel : MaxBaseLevel;

    public static int MaxSkillLevel(string type) => LegacySkills.Contains(type) ? MaxLegacySkill : MaxSkill;

    /// <summary>Whether the skill exists at this patch: the old ones before 2.0, the new ones from it, and the Relic skill only with the expansion.</summary>
    public bool HasSkill(string type) => type == RelicSkill ? HasPhantomLiberty : LegacySkills.Contains(type) != HasNewSkills;

    /// <summary>The patches this save can be moved to without converting its data: later ones with the same save version.</summary>
    public IEnumerable<GamePatch> ReachablePatches(uint saveVersion) => Patches.Where(p => p.GameVersion > GameVersion && p.SaveVersion == saveVersion);

    /// <summary>
    /// A guess for a loaded save: a level above the base cap, Relic points, or a Relic skill above its starting level can only come from the
    /// expansion. A fresh character on the base game looks the same as one on the expansion, so the user can change it.
    /// </summary>
    public static bool LooksLikePhantomLiberty(PlayerDevelopment dev) =>
        dev.Level?.Level > MaxBaseLevel
        || dev.Proficiencies.Any(p => p.Type == RelicSkill && p.Level > 1)
        || dev.Points.Any(p => p.Type == RelicSkill && p.Unspent > 0)
        || dev.PerkEntries.Any(e => e.Tree == Perks.RelicTree && e.Level > 0);
}
