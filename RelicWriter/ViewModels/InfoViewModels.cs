using RelicWriter.Common.Codecs.Generated;
using RelicWriter.Common.Container;

namespace RelicWriter.ViewModels;

public sealed record InfoRow(string Label, string Value);

/// <summary>Read-only facts about the file: versions, timestamp and size.</summary>
public sealed class SaveInfoViewModel : ViewModelBase
{
    const double BytesPerMegabyte = 1024.0 * 1024.0;

    public SaveInfoViewModel(SaveFile file, string path, long size)
    {
        var h = file.Header;
        Rows =
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

    public IReadOnlyList<InfoRow> Rows { get; }
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
