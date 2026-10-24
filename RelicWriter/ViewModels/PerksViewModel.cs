using System.Collections.ObjectModel;
using Avalonia;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicWriter.Common.Model;

namespace RelicWriter.ViewModels;

/// <summary>One perk: its bought level, and the points that buying or selling levels takes from or gives back to the character.</summary>
public partial class PerkViewModel : ViewModelBase
{
    readonly PlayerDevelopment dev;
    readonly Action<PerkInfo, int> spent;
    readonly Func<IEnumerable<PerkViewModel>> all;
    readonly Action<PerkInfo> needsAttribute;
    readonly Func<PerkInfo, int, bool> canBuy;

    public PerkViewModel(PlayerDevelopment dev, PerkInfo info, Action<PerkInfo, int> spent, Func<IEnumerable<PerkViewModel>> all, Action<PerkInfo> needsAttribute, Func<PerkInfo, int, bool> canBuy)
    {
        this.dev = dev;
        Info = info;
        this.spent = spent;
        this.all = all;
        this.needsAttribute = needsAttribute;
        this.canBuy = canBuy;
        Level = dev.PerkLevel(info);
    }

    public PerkInfo Info { get; }

    public int MaxLevel => Info.MaxLevel;

    // The in-game English name when the game files are found; else "Reflexes_Left_Perk_2_3" reads as "Left perk 2-3".
    public string Title => Info.DisplayName ?? Regex.Replace(Info.Id[(Info.Tree.Length + 1)..].Replace('_', ' '), @"(\d) (\d)", "$1-$2");

    public string CostText => Info.CostPerLevel == 1 ? "" : $"{Info.CostPerLevel} points per level";

    // Where the perk is drawn in the tree (set by PerkLayout), and how big.
    public double X { get; set; }
    public double Y { get; set; }
    public double Size { get; set; }
    public bool IsHub { get; set; }
    public bool LabelAbove { get; set; }
    public bool LabelBelow => !LabelAbove;
    public double LabelOffset => Size + 3;
    public double LabelOffsetUp => -(Size + 3);
    public bool IsRelic => Info.IsRelic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBought), nameof(IsMaxed), nameof(LevelText))]
    public partial double Level { get; set; }

    public bool IsBought => Level > 0;
    public bool IsMaxed => Level >= MaxLevel;
    public string LevelText => MaxLevel > 1 ? $"{Level:0}/{MaxLevel}" : "";

    /// <summary>What the game wants before this perk can be bought, for the detail card.</summary>
    public string RequirementText
    {
        get
        {
            var needs = all().Where(p => Info.RequiredPerks.Contains(p.Info.Id)).Select(p => p.Title).ToList();
            var parts = new List<string>();
            if (Info.AttributePoints > 0) parts.Add($"{Perks.AttributeStat(Info.Tree)} at {Info.AttributePoints}".ToUpperInvariant());
            if (needs.Count > 0) parts.Add("Needs " + string.Join(", ", needs));
            return parts.Count == 0 ? "No requirements" : string.Join("  ·  ", parts);
        }
    }

    public string CardCost => $"{Info.CostPerLevel * MaxLevel} {(Info.IsRelic ? "relic" : "perk")} point{(Info.CostPerLevel * MaxLevel == 1 ? "" : "s")} in all";

    public void Raise() => Level = Math.Min(MaxLevel, Math.Round(Level) + 1);

    public void Lower() => Level = Math.Max(0, Math.Round(Level) - 1);

    partial void OnLevelChanged(double oldValue, double newValue)
    {
        var target = (int)Math.Round(newValue);
        var before = dev.PerkLevel(Info);
        if (target == before) { if (newValue != before) Level = before; return; }

        // The game buys a perk only with the points for it, the perks it requires at their top level and the attribute at its tier's value.
        if (target > before)
        {
            if (!canBuy(Info, target)) { Level = before; return; }
            needsAttribute(Info);
            foreach (var p in all().Where(p => Info.RequiredPerks.Contains(p.Info.Id))) p.Level = p.MaxLevel;
        }
        dev.SetPerkLevel(Info, target);
        spent(Info, target - before);
        // Selling a level of a perk takes back the perks that need it at its top level.
        if (target < MaxLevel)
            foreach (var p in all().Where(p => p.Info.RequiredPerks.Contains(Info.Id))) p.Level = 0;
    }
}

/// <summary>A line from a perk to the perk that needs it, lit while the perk it leaves is bought.</summary>
public sealed class PerkLink : ViewModelBase
{
    readonly PerkViewModel from;

    public PerkLink(PerkViewModel from, PerkViewModel to)
    {
        this.from = from;
        // Along the rail out of the first perk, then across to the second.
        Points = [new Point(from.X + from.Size / 2, from.Y + from.Size / 2), new Point(from.X + from.Size / 2, to.Y + to.Size / 2), new Point(to.X + to.Size / 2, to.Y + to.Size / 2)];
        from.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PerkViewModel.IsBought)) OnPropertyChanged(nameof(IsOn)); };
    }

    public IReadOnlyList<Point> Points { get; }
    public bool IsOn => from.IsBought;
    public bool IsRelic => from.IsRelic;
}

public sealed record PerkTreeViewModel(string Title, IReadOnlyList<PerkViewModel> Perks, IReadOnlyList<PerkLink> Links)
{
    public double Width => PerkLayout.Width;
    public double Height => PerkLayout.Height;
}

/// <summary>The perks tab: every perk of the attribute trees and the Relic tree, with the unspent points kept in step.</summary>
public partial class PerksViewModel : ViewModelBase
{
    readonly PlayerDevelopment dev;
    readonly CharacterViewModel character;
    readonly Action changed;

    public PerksViewModel(PlayerDevelopment dev, CharacterViewModel character, Action changed)
    {
        this.dev = dev;
        this.character = character;
        this.changed = changed;
        Trees = new ObservableCollection<PerkTreeViewModel>(Perks.TreeData.Keys.Where(dev.HasTree).Select(tree =>
        {
            var perks = Perks.All.Where(p => p.Tree == tree).Select(p => new PerkViewModel(dev, p, Spend, () => Trees.SelectMany(t => t.Perks), RaiseAttribute, CanBuy)).ToList();
            PerkLayout.Place(perks);
            var byId = perks.ToDictionary(p => p.Info.Id);
            var links = perks.SelectMany(to => to.Info.RequiredPerks.Where(byId.ContainsKey).Select(id => new PerkLink(byId[id], to))).ToList();
            return new PerkTreeViewModel(tree == Perks.RelicTree ? "RELIC" : tree.ToUpperInvariant(), perks, links);
        }));

        // Lowering an attribute takes back the perks its new value no longer unlocks (the points come back through Spend).
        foreach (var attribute in character.Attributes)
            attribute.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(AttributeViewModel.Value)) return;
                foreach (var perk in Trees.SelectMany(t => t.Perks).Where(p => Perks.AttributeStat(p.Info.Tree) == attribute.Stat && p.Info.AttributePoints > attribute.Value && p.Level > 0))
                    perk.Level = 0;
            };
    }

    public ObservableCollection<PerkTreeViewModel> Trees { get; }

    /// <summary>The perk the pointer is over, shown on the detail card.</summary>
    [ObservableProperty] public partial PerkViewModel? SelectedPerk { get; set; }

    public bool CanEditPerkPoints => character.CanEditPerkPoints;
    public bool CanEditRelicPoints => character.CanEditRelicPoints && dev.HasRelic;

    public string OfficialMaxText => $"OFFICIAL MAX ({Perks.OfficialMaxPerkPoints})";

    // The game unlocks a perk only when its attribute has reached the tier's value, so buying one raises the attribute to it.
    void RaiseAttribute(PerkInfo perk)
    {
        if (character.Attributes.FirstOrDefault(a => a.Stat == Perks.AttributeStat(perk.Tree)) is { } attribute && attribute.Value < perk.AttributePoints) attribute.Value = perk.AttributePoints;
    }

    // Points for this perk up to the level, plus the missing levels of everything it requires; a character without that points entry is not limited.
    bool CanBuy(PerkInfo perk, int level)
    {
        var points = perk.IsRelic ? character.RelicPoints : character.PerkPoints;
        if (points is null) return true;
        var all = Trees.SelectMany(t => t.Perks).ToDictionary(p => p.Info.Id);
        var needed = (level - dev.PerkLevel(perk)) * perk.CostPerLevel;
        var pending = new Stack<PerkInfo>(perk.RequiredPerks.Select(r => all[r].Info));
        for (var seen = new HashSet<string> { perk.Id }; pending.Count > 0;)
            if (pending.Pop() is var p && seen.Add(p.Id))
            {
                needed += (p.MaxLevel - dev.PerkLevel(p)) * p.CostPerLevel;
                foreach (var r in p.RequiredPerks) pending.Push(all[r].Info);
            }
        return needed <= points;
    }

    // Buying a level takes its cost from the unspent points, selling gives it back; the points never go below zero.
    void Spend(PerkInfo perk, int levels)
    {
        var cost = levels * perk.CostPerLevel;
        if (perk.IsRelic) character.RelicPoints = Math.Max(0, (character.RelicPoints ?? 0) - cost);
        else character.PerkPoints = Math.Max(0, (character.PerkPoints ?? 0) - cost);
        changed();
    }

    /// <summary>Points still needed to buy every level of the regular perks, or of the Relic perks.</summary>
    int Remaining(bool relic) => Perks.All.Where(p => p.IsRelic == relic && dev.HasTree(p.Tree)).Sum(p => (p.MaxLevel - dev.PerkLevel(p)) * p.CostPerLevel);

    // Each level bought takes its cost from the unspent points, as in the game.
    [RelayCommand]
    void MaxOutAllPerks()
    {
        // The points for the missing levels are added first, so the unspent points end up as they were.
        if (character.CanEditPerkPoints) character.PerkPoints = (character.PerkPoints ?? 0) + Remaining(false);
        if (character.CanEditRelicPoints && dev.HasRelic) character.RelicPoints = (character.RelicPoints ?? 0) + Remaining(true);
        foreach (var perk in Trees.SelectMany(t => t.Perks)) perk.Level = perk.MaxLevel;
    }

    [RelayCommand(CanExecute = nameof(CanEditPerkPoints))]
    void PointsForAllPerks() => character.PerkPoints = Remaining(false);

    [RelayCommand(CanExecute = nameof(CanEditPerkPoints))]
    void PointsForOfficialMax() => character.PerkPoints = Perks.OfficialMaxPerkPoints;

    [RelayCommand(CanExecute = nameof(CanEditRelicPoints))]
    void PointsForAllRelicPerks() => character.RelicPoints = Remaining(true);
}
