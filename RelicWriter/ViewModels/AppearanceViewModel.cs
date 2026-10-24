using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;

namespace RelicWriter.ViewModels;

/// <summary>One character-creator slot that holds a preset number (eyes, nose, mouth, jaw).</summary>
public sealed partial class FacePresetViewModel(Appearance appearance, string slot, Action changed) : ObservableObject
{
    /// <summary>shortcut: the game's preset range was not read from its files, so this is wide enough for the numbers seen in saves (up to 64); narrow it when the creator's option tables are read.</summary>
    public const int Min = 1, Max = 99;

    public string Title { get; } = Humanize(slot).ToUpperInvariant();

    public decimal Value
    {
        get => appearance.Preset(slot) ?? Min;
        set
        {
            var number = (int)Math.Clamp(value, Min, Max);
            if (number == Value) return;
            appearance.SetPreset(slot, number);
            OnPropertyChanged();
            changed();
        }
    }

    internal static string Humanize(string tag)
    {
        var words = CamelBoundary().Replace(tag.Trim('_'), "$1 $2").Replace('_', ' ').Trim();
        words = TrailingNumber().Replace(words, "").Trim();
        return words.Length == 0 ? tag : char.ToUpperInvariant(words[0]) + words[1..];
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex(@"\s*\d+$")]
    private static partial Regex TrailingNumber();
}

/// <summary>A part the creator filled: what it is (hair colour, eye colour, a tattoo...) and which variant.</summary>
public sealed record AppearancePartViewModel(string Title, string Value);

public sealed class AppearanceViewModel : ViewModelBase
{
    /// <summary>Parts for the first-person view repeat the third-person ones; they are left out of the list.</summary>
    const string FirstPersonMarker = "fpp";
    const string VariantSeparator = "__";

    public AppearanceViewModel(Appearance appearance, Action changed)
    {
        FaceShape = appearance.PresetSlots.Select(s => new FacePresetViewModel(appearance, s, changed)).ToList();
        Parts = appearance.CreatorParts
            .Where(p => p.Tag.Length > 0 && !p.Tag.Contains(FirstPersonMarker, StringComparison.OrdinalIgnoreCase))
            .Select(p => new AppearancePartViewModel(FacePresetViewModel.Humanize(p.Tag), Variant(p.Resource)))
            .ToList();
        GameFolder = GameLocator.Find();
    }

    public IReadOnlyList<FacePresetViewModel> FaceShape { get; }

    public IReadOnlyList<AppearancePartViewModel> Parts { get; }

    public bool HasFaceShape => FaceShape.Count > 0;

    /// <summary>Where the game was found, or null; the creator's options and models will be read from there.</summary>
    public string? GameFolder { get; }

    public string GameFolderText => GameFolder ?? "Game not found.";

    /// <summary>"he_000_pwa__basehead__62_cyber_eye" becomes "cyber eye": the last section, without its leading number.</summary>
    internal static string Variant(string resource)
    {
        var at = resource.LastIndexOf(VariantSeparator, StringComparison.Ordinal);
        var last = at < 0 ? resource : resource[(at + VariantSeparator.Length)..];
        return Regex.Replace(last, @"^\d+_", "").Replace('_', ' ');
    }
}
