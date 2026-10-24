using System.Collections.ObjectModel;
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

    public PerkViewModel(PlayerDevelopment dev, PerkInfo info, Action<PerkInfo, int> spent)
    {
        this.dev = dev;
        Info = info;
        this.spent = spent;
        Level = dev.PerkLevel(info);
    }

    public PerkInfo Info { get; }

    public int MaxLevel => Info.MaxLevel;

    // The in-game English name when the game files are found; else "Reflexes_Left_Perk_2_3" reads as "Left perk 2-3".
    public string Title => Info.DisplayName ?? Regex.Replace(Info.Id[(Info.Tree.Length + 1)..].Replace('_', ' '), @"(\d) (\d)", "$1-$2");

    public string CostText => Info.CostPerLevel == 1 ? "" : $"{Info.CostPerLevel} points per level";

    [ObservableProperty] public partial decimal? Level { get; set; }

    partial void OnLevelChanged(decimal? oldValue, decimal? newValue)
    {
        // A cleared box (null) is ignored so a half-typed number never reaches the save.
        if (newValue is null) return;
        var before = dev.PerkLevel(Info);
        dev.SetPerkLevel(Info, (int)newValue.Value);
        var after = dev.PerkLevel(Info);
        if (after != before) spent(Info, after - before);
        if (after != (int)newValue.Value) Level = after;
    }
}

public sealed record PerkTreeViewModel(string Title, IReadOnlyList<PerkViewModel> Perks);

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
            new PerkTreeViewModel(tree == Perks.RelicTree ? "RELIC" : tree.ToUpperInvariant(),
                Perks.All.Where(p => p.Tree == tree).Select(p => new PerkViewModel(dev, p, Spend)).ToList())));
    }

    public ObservableCollection<PerkTreeViewModel> Trees { get; }

    public bool CanEditPerkPoints => character.CanEditPerkPoints;
    public bool CanEditRelicPoints => character.CanEditRelicPoints && dev.HasRelic;

    public string OfficialMaxText => $"OFFICIAL MAX ({Perks.OfficialMaxPerkPoints})";

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

    // Each level bought takes its cost from the unspent points, so they run down to zero like in the game.
    [RelayCommand]
    void MaxOutAllPerks()
    {
        foreach (var perk in Trees.SelectMany(t => t.Perks)) perk.Level = perk.MaxLevel;
    }

    [RelayCommand(CanExecute = nameof(CanEditPerkPoints))]
    void PointsForAllPerks() => character.PerkPoints = Remaining(false);

    [RelayCommand(CanExecute = nameof(CanEditPerkPoints))]
    void PointsForOfficialMax() => character.PerkPoints = Perks.OfficialMaxPerkPoints;

    [RelayCommand(CanExecute = nameof(CanEditRelicPoints))]
    void PointsForAllRelicPerks() => character.RelicPoints = Remaining(true);
}
