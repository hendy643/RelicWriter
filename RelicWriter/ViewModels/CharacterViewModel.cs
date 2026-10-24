using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicWriter.Common.Model;

namespace RelicWriter.ViewModels;

public partial class AttributeViewModel : ViewModelBase
{
    public const int Min = 3, Max = 20;

    static readonly Dictionary<string, string> Titles = new()
    {
        ["Strength"] = "BODY", ["Reflexes"] = "REFLEXES", ["TechnicalAbility"] = "TECHNICAL ABILITY", ["Intelligence"] = "INTELLIGENCE", ["Cool"] = "COOL",
    };

    readonly AttributeEntry entry;
    Action? changed;

    public AttributeViewModel(AttributeEntry entry, Action changed)
    {
        this.entry = entry;
        Value = entry.Value;
        this.changed = changed;
    }

    public string Stat => entry.Stat;

    public string Title => Titles.GetValueOrDefault(entry.Stat, entry.Stat.ToUpperInvariant());

    [ObservableProperty] public partial double Value { get; set; }

    partial void OnValueChanged(double value)
    {
        entry.Value = (int)Math.Round(value);
        changed?.Invoke();
    }
}

public partial class SkillViewModel : ViewModelBase
{
    public const int Min = 1, Max = 60;

    readonly ProficiencyEntry entry;
    Action? changed;

    public SkillViewModel(ProficiencyEntry entry, Action changed)
    {
        this.entry = entry;
        Value = entry.Level;
        this.changed = changed;
    }

    // Names as the game's skill screen shows them (from memory, not read from the game files); "Espionage" is the Phantom Liberty Relic skill.
    static readonly Dictionary<string, string> Titles = new()
    {
        ["Brawling"] = "STREET BRAWLER", ["Gunslinger"] = "HANDGUNS", ["Kenjutsu"] = "BLADES", ["CombatHacking"] = "BREACH PROTOCOL", ["Hacking"] = "QUICKHACKING",
        ["Demolition"] = "ANNIHILATION", ["Espionage"] = "RELIC",
    };

    /// <summary>The in-game skill name where known, else the internal proficiency name split into words.</summary>
    public string Title => Titles.TryGetValue(entry.Type, out var t) ? t : Regex.Replace(entry.Type, "(?<=[a-z])(?=[A-Z])", " ").ToUpperInvariant();

    [ObservableProperty] public partial double Value { get; set; }

    partial void OnValueChanged(double value)
    {
        entry.Level = (int)Math.Round(value);
        changed?.Invoke();
    }
}

/// <summary>The character screen: level, street cred, life path, attributes, skills and unspent points.</summary>
public partial class CharacterViewModel : ViewModelBase
{
    public const int MaxLevel = 60, MaxStreetCred = 50, MaxPoints = 999;

    readonly DevelopmentPointsEntry? attributePoints, perkPoints, relicPoints;
    readonly ProficiencyEntry? level, streetCred;
    Action? changed;

    public CharacterViewModel(PlayerDevelopment dev, Action changed)
    {
        level = dev.Level;
        streetCred = dev.StreetCred;
        attributePoints = dev.Points.FirstOrDefault(p => p.Type == "Secondary");
        perkPoints = dev.Points.FirstOrDefault(p => p.Type == "Primary");
        relicPoints = dev.Points.FirstOrDefault(p => p.Type == "Espionage");
        LifePath = dev.LifePath.ToUpperInvariant();
        Level = level?.Level;
        StreetCred = streetCred?.Level;
        AttributePoints = attributePoints?.Unspent;
        PerkPoints = perkPoints?.Unspent;
        RelicPoints = relicPoints?.Unspent;
        Attributes = new ObservableCollection<AttributeViewModel>(dev.Attributes.Select(a => new AttributeViewModel(a, changed)));
        Skills = new ObservableCollection<SkillViewModel>(dev.Skills.OrderBy(s => s.Type).Select(s => new SkillViewModel(s, changed)));
        this.changed = changed;
    }

    public string LifePath { get; }

    public ObservableCollection<AttributeViewModel> Attributes { get; }

    public ObservableCollection<SkillViewModel> Skills { get; }

    public bool CanEditLevel => level is not null;
    public bool CanEditStreetCred => streetCred is not null;
    public bool CanEditAttributePoints => attributePoints is not null;
    public bool CanEditPerkPoints => perkPoints is not null;
    public bool CanEditRelicPoints => relicPoints is not null;

    [ObservableProperty] public partial decimal? Level { get; set; }
    [ObservableProperty] public partial decimal? StreetCred { get; set; }
    [ObservableProperty] public partial decimal? AttributePoints { get; set; }
    [ObservableProperty] public partial decimal? PerkPoints { get; set; }
    [ObservableProperty] public partial decimal? RelicPoints { get; set; }

    partial void OnLevelChanged(decimal? value) => Store(level, value, v => level!.Level = v);
    partial void OnStreetCredChanged(decimal? value) => Store(streetCred, value, v => streetCred!.Level = v);
    partial void OnAttributePointsChanged(decimal? value) => Store(attributePoints, value, v => attributePoints!.Unspent = v);
    partial void OnPerkPointsChanged(decimal? value) => Store(perkPoints, value, v => perkPoints!.Unspent = v);
    partial void OnRelicPointsChanged(decimal? value) => Store(relicPoints, value, v => relicPoints!.Unspent = v);

    // A cleared box (null) is ignored so a half-typed number never reaches the save.
    void Store(object? target, decimal? value, Action<int> set)
    {
        if (target is null || value is null) return;
        set((int)value.Value);
        changed?.Invoke();
    }
}
