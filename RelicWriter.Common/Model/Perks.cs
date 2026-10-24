using RelicWriter.Common.GameData;

namespace RelicWriter.Common.Model;

/// <summary>One perk: its tree, how many levels it has and what each level costs. <paramref name="Requires"/> lists, space separated, the perks that must be at their top level first. <paramref name="AttributePoints"/> is the attribute value the perk's tier needs before it can be bought.</summary>
public sealed record PerkInfo(string Id, string Tree, int MaxLevel, int CostPerLevel, string? DisplayName = null, string Requires = "", int AttributePoints = 0)
{
    public string[] RequiredPerks => Requires.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public bool IsRelic => Tree == Perks.RelicTree;

    public int TotalCost => MaxLevel * CostPerLevel;
}

/// <summary>
/// The perks a character can buy, in the game's order, read from the game database. Costs follow the game's script: one perk point per level,
/// and in the Relic tree one Relic point per level except the three milestone perks, which cost three.
/// </summary>
public static class Perks
{
    public const string RelicTree = "Espionage";

    /// <summary>The save's attribute stat a tree's perks need points in (the trees are named Body and Tech, the stats Strength and TechnicalAbility); the Relic tree has none.</summary>
    public static string AttributeStat(string tree) => tree switch { "Body" => "Strength", "Tech" => "TechnicalAbility", _ => tree };

    /// <summary>Perk points a character can earn in a normal game with Phantom Liberty (59 from levelling to 60, 10 from skills, 13 from Perk Shards).</summary>
    public static int OfficialMaxPerkPoints => GameDatabase.Current.Constants[GameDatabase.OfficialMaxPerkPoints];

    /// <summary>The trees in the game's order, with the names the save gives their attribute data.</summary>
    public static IReadOnlyDictionary<string, string> TreeData => GameDatabase.Current.PerkTrees;

    public static IReadOnlyList<PerkInfo> All => GameDatabase.Current.Perks;

    /// <summary>Perk points to buy every regular perk, or Relic points to buy every Relic perk.</summary>
    public static int CostOfAll(bool relic) => All.Where(p => p.IsRelic == relic).Sum(p => p.TotalCost);
}
