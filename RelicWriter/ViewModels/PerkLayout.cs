using System.Text.RegularExpressions;
using Avalonia;

namespace RelicWriter.ViewModels;

/// <summary>
/// Where each perk sits in the tree picture. The game's files give no coordinates, so this follows the perk ids: "Left_Milestone_2" is the hub of the left
/// column at the second tier, "Central_Perk_3_1" a perk around the central hub of tier 3, "Inbetween_Left_3" sits between two columns, "Master_Perk_1" tops the tree.
/// The order of the four perks around a hub (slot 1 to 4) is a guess. Anything that does not match is put in the row of its tier.
/// </summary>
public static partial class PerkLayout
{
    public const double Width = 920, Height = 770;
    public const double HubSize = 76, PerkSize = 54;
    const double RowHeight = 205, TopY = 44, LeafX = 90, LeafY = 40;
    static readonly Dictionary<string, double> Columns = new() { ["Left"] = 150, ["Central"] = 470, ["Right"] = 790 };

    [GeneratedRegex(@"^(Left|Central|Right)_Milestone(?:_(\d))?")] private static partial Regex Hub();
    [GeneratedRegex(@"^(Left|Central|Right)_Perk_(\d)_(\d+)$")] private static partial Regex Leaf();
    [GeneratedRegex(@"^Inbetween_(Left|Right)_(\d)$")] private static partial Regex Between();
    [GeneratedRegex(@"^Master_Perk_(\d)$")] private static partial Regex Master();

    static double RowY(int tier) => TopY + (4 - Math.Clamp(tier, 1, 4)) * RowHeight;

    public static void Place(IReadOnlyList<PerkViewModel> perks)
    {
        var centres = new Dictionary<string, Point>();
        var taken = new List<(Point Centre, double Size)>();
        // Hubs first, then what hangs on them, then what sits between, then the masters, which sit over their parents.
        foreach (var pass in new[] { 0, 1, 2, 3, 4 })
            foreach (var perk in perks)
            {
                var name = perk.Info.Id[(perk.Info.Tree.Length + 1)..];
                if (Order(name, perk) != pass) continue;
                var centre = Centre(name, perk, centres);
                perk.IsHub = name.Contains("Milestone") || name.StartsWith("Master");
                // The upper two perks around a hub carry their name above them, the lower two below, so the tiers can sit close together.
                perk.LabelAbove = Leaf().Match(name) is { Success: true } leaf && (int.Parse(leaf.Groups[3].Value) - 1) % 4 < 2;
                perk.Size = perk.IsHub ? HubSize : PerkSize;
                // Overlapping another perk (a column with more perks than slots, two masters over one hub): slide right until clear.
                while (taken.FirstOrDefault(t => Math.Abs(t.Centre.X - centre.X) < (t.Size + perk.Size) / 2 && Math.Abs(t.Centre.Y - centre.Y) < (t.Size + perk.Size) / 2) is { Size: > 0 } hit)
                    centre = new Point(centre.X + (hit.Size + perk.Size) / 2 + 6, centre.Y);
                taken.Add((centre, perk.Size));
                centres[perk.Info.Id] = centre;
                perk.X = centre.X - perk.Size / 2;
                perk.Y = centre.Y - perk.Size / 2;
            }
    }

    static int Order(string name, PerkViewModel perk) =>
        Hub().IsMatch(name) ? 0 : Leaf().IsMatch(name) ? 1 : Between().IsMatch(name) ? 2 : Master().IsMatch(name) ? 3 : 4;

    static Point Centre(string name, PerkViewModel perk, Dictionary<string, Point> placed)
    {
        if (Hub().Match(name) is { Success: true } h) return new Point(Columns[h.Groups[1].Value], RowY(h.Groups[2].Success ? int.Parse(h.Groups[2].Value) : 1));
        if (Leaf().Match(name) is { Success: true } l)
        {
            var slot = (int.Parse(l.Groups[3].Value) - 1) % 4;
            return new Point(Columns[l.Groups[1].Value] + (slot % 2 == 0 ? -LeafX : LeafX), RowY(int.Parse(l.Groups[2].Value)) + (slot < 2 ? -LeafY : LeafY));
        }
        if (Between().Match(name) is { Success: true } b)
            return new Point(b.Groups[1].Value == "Left" ? (Columns["Left"] + Columns["Central"]) / 2 : (Columns["Central"] + Columns["Right"]) / 2, RowY(int.Parse(b.Groups[2].Value)));
        var parents = perk.Info.RequiredPerks.Where(placed.ContainsKey).Select(p => placed[p].X).ToList();
        var tier = Master().IsMatch(name) ? 4 : perk.Info.AttributePoints switch { >= 20 => 4, >= 15 => 3, >= 9 => 2, _ => 1 };
        return new Point(parents.Count > 0 ? parents.Average() : Columns["Central"], RowY(tier));
    }
}
