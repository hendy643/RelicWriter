using RelicWriter.Common.Codecs.Generated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicWriter.Common.Container;
using RelicWriter.Common.Model;

namespace RelicWriter.ViewModels;

public sealed record InfoRow(string Label, string Value);

/// <summary>The file's versions, timestamp and size, and the game patch the editor allows edits for (it can be moved up to a later patch whose saves have the same layout).</summary>
public sealed partial class SaveInfoViewModel : ViewModelBase
{
    const double BytesPerMegabyte = 1024.0 * 1024.0;

    readonly SaveFile file;
    readonly string path;
    readonly long size;
    readonly EditionViewModel edition;
    readonly Action changed;
    readonly bool expansionEvident;

    public SaveInfoViewModel(SaveFile file, string path, long size, EditionViewModel edition, bool expansionEvident, Action changed)
    {
        this.file = file;
        this.path = path;
        this.size = size;
        this.edition = edition;
        this.expansionEvident = expansionEvident;
        this.changed = changed;
        Rows = BuildRows();
        Choices = edition.Edition.ReachablePatches(file.Header.SaveVersion).ToList();
    }

    [ObservableProperty] public partial IReadOnlyList<InfoRow> Rows { get; set; }

    /// <summary>Later patches that write the same save version, so moving there changes only the header's game version.</summary>
    [ObservableProperty] public partial IReadOnlyList<GamePatch> Choices { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ApplyPatchCommand))] public partial GamePatch? SelectedPatch { get; set; }

    public bool HasChoices => Choices.Count > 0;

    partial void OnChoicesChanged(IReadOnlyList<GamePatch> value) => OnPropertyChanged(nameof(HasChoices));

    [RelayCommand(CanExecute = nameof(CanApplyPatch))]
    void ApplyPatch()
    {
        var patch = SelectedPatch!;
        file.Header = file.Header.WithGameVersion(patch.GameVersion);
        edition.Edition = edition.Edition with { GameVersion = patch.GameVersion };
        Rows = BuildRows();
        Choices = edition.Edition.ReachablePatches(file.Header.SaveVersion).ToList();
        SelectedPatch = null;
        OnPropertyChanged(nameof(CanChangeExpansion));
        OnPropertyChanged(nameof(PhantomLiberty));
        OnPropertyChanged(nameof(UpgradeNote));
        changed();
    }

    bool CanApplyPatch() => SelectedPatch is not null;

    /// <summary>What to do to get a save from before 2.0 onto the 2.x rules: the game converts it itself, RelicWriter cannot.</summary>
    public string UpgradeNote => edition.Edition.GameVersion < GameEdition.SkillRework
        ? "This save is from before 2.0, which rebuilt the perk trees, merged the skills into five new ones and added Cyberware Capacity. The game converts a save to that when it loads it " +
          "(spent perk points are refunded, old skill progress is carried over). RelicWriter cannot do that conversion. To use the 2.x features, load this save in the current game, " +
          "save again, and open the new file here."
        : "From 2.0 on, every patch has the same limits and features; only Phantom Liberty changes them. The patches offered above differ only in the game version in the file header.";

    /// <summary>The expansion is not stored in a save, so this only decides what the editor allows. Only 2.0 and later have it, and a save that shows signs of it keeps it.</summary>
    public bool PhantomLiberty
    {
        get => edition.Edition.PhantomLiberty;
        set
        {
            if (value == edition.Edition.PhantomLiberty || !CanChangeExpansion) return;
            edition.Edition = edition.Edition with { PhantomLiberty = value };
            OnPropertyChanged();
        }
    }

    public bool CanChangeExpansion => edition.Edition.GameVersion >= GameEdition.SkillRework && !expansionEvident;

    List<InfoRow> BuildRows()
    {
        var h = file.Header;
        return
        [
            new("FILE", path),
            new("SIZE", $"{size / BytesPerMegabyte:N2} MB"),
            new("WRITTEN BY GAME VERSION", $"{h.GameVersionLabel}  (game version {h.GameVersion})"),
            new("SAVE VERSION", h.SaveVersion.ToString()),
            new("ARCHIVE VERSION", h.ArchiveVersion.ToString()),
            new("SAVED", h.Timestamp?.ToString("f") ?? "unknown"),
            new("NODES", file.Walk().Count().ToString("N0")),
        ];
    }
}

public sealed class NodeViewModel(SaveNode node, uint saveVersion) : ViewModelBase
{
    public string Name => node.Name;

    public string Summary => $"{node.Data.Length + node.Trailing.Length:N0} bytes" + (node.Children.Count > 0 ? $" · {node.Children.Count} children" : "");

    public string Details =>
        $"Own data {node.Data.Length:N0} bytes\nData after children {node.Trailing.Length:N0} bytes\nChildren {node.Children.Count}\n" +
        $"Expected in save version {saveVersion}: {(NodeAvailability.IsExpected(node.Name, saveVersion) ? "yes" : "no (new node)")}\n" +
        $"Typed codec: {(GeneratedCodecs.RoundTrips.ContainsKey(node.Name) ? "yes" : "no, kept as raw bytes")}";

    public IReadOnlyList<NodeViewModel> Children { get; } = node.Children.Select(c => new NodeViewModel(c, saveVersion)).ToList();
}

/// <summary>Read-only browser for the node tree, to see what a save contains and which parts RelicWriter can edit.</summary>
public sealed partial class NodesViewModel(SaveFile file) : ViewModelBase
{
    public IReadOnlyList<NodeViewModel> Roots { get; } = file.Roots.Select(r => new NodeViewModel(r, file.Header.SaveVersion)).ToList();

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] public partial NodeViewModel? Selected { get; set; }
}
