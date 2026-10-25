using RelicWriter.Common.AppInfo;

namespace RelicWriter.ViewModels;

public sealed record CreditLine(string Title, string Text);

/// <summary>The About tab: the version, who made it, what it stands on, and the change log (CHANGELOG.md, read at build time).</summary>
public sealed class AboutViewModel : ViewModelBase
{
    const string RepositoryUrl = "https://github.com/hendy643/RelicWriter";

    public string Version => AppVersion.DisplayText;

    /// <summary>Builds that are not a formal release say so, and which commit they came from.</summary>
    public string BuildNote => AppVersion.Revision is { } hash ? $"Development build from commit {hash}, not a formal release." : "Release build.";

    public string Repository => RepositoryUrl;

    public IReadOnlyList<CreditLine> Credits { get; } =
    [
        new("Author", "hendy643, leading a team of one human and two AIs: Claude (Claude Code, by Anthropic), which wrote most of the code, tests and documentation, and GitHub Copilot."),
        new("CD PROJEKT RED", "Cyberpunk 2077 and Phantom Liberty are theirs, as are the game's names, numbers and pictures. RelicWriter is an unofficial fan project, not affiliated with or endorsed by CD PROJEKT S.A. or CD PROJEKT RED. Pictures are read from your own copy of the game."),
        new("Save format research", "CyberCAT, CyberpunkSaveEditor, CyberCAT-SimpleGUI and CP2077SaveEditor (all MIT), the Red Modding wiki, WolvenKit's documentation and Ghidra."),
        new("Libraries", "Avalonia, CommunityToolkit.Mvvm, K4os.Compression.LZ4, BCnEncoder.Net, Entity Framework Core with SQLite, and xUnit.net. Fonts: Rajdhani and Inter."),
        new("Licence", "RelicWriter is free software under the GPL-3.0. The game text in its database belongs to CD PROJEKT RED. The full list is in docs/credits.md."),
    ];

    /// <summary>CHANGELOG.md, turned into data when the app was built.</summary>
    public IReadOnlyList<ChangelogEntry> Changelog => ChangelogData.Entries;
}
