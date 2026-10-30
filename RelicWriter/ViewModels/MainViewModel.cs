using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicWriter.Common.Container;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;
using RelicWriter.Common.AppInfo;
using RelicWriter.GameAssets;

namespace RelicWriter.ViewModels;

/// <summary>Asks the user for a file; implemented by the window so the view model stays free of UI types.</summary>
public interface IFileDialogs
{
    Task<string?> PickOpenAsync();

    Task<string?> PickFolderAsync();

    Task<string?> PickSaveAsync(string suggestedFileName);

    Task ShowBugReportAsync(string text);

    Task OpenUrlAsync(string url);
}

public sealed record RecentSave(string Name, string Path, DateTime Modified)
{
    public string ModifiedText => Modified.ToString("g");
}

public partial class MainViewModel : ViewModelBase
{
    SaveFile? file;
    PlayerDevelopment? development;
    Inventory? inventory;
    VehicleGarage? garage;
    Appearance? appearance;
    readonly HashSet<string> backedUp = [];

    public IFileDialogs? Dialogs { get; set; }

    public ObservableCollection<RecentSave> RecentSaves { get; } = [];

    public bool HasRecentSaves => RecentSaves.Count > 0;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveAsCommand))]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty] public partial bool IsDirty { get; set; }
    [ObservableProperty] public partial string? FilePath { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "Open a save to begin.";
    [ObservableProperty] public partial bool StatusIsError { get; set; }
    [ObservableProperty] public partial CharacterViewModel? Character { get; set; }
    [ObservableProperty] public partial PerksViewModel? Perks { get; set; }
    [ObservableProperty] public partial AppearanceViewModel? Appearance { get; set; }
    [ObservableProperty] public partial PreviewViewModel? Preview { get; set; }

    /// <summary>Set by the view: whether the Appearance tab is the one showing.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowPreview))] public partial bool AppearanceTabSelected { get; set; }

    /// <summary>The preview window is open exactly while the Appearance tab is showing for a loaded save that has an appearance.</summary>
    public bool ShowPreview => IsLoaded && AppearanceTabSelected && Preview is not null;

    partial void OnIsLoadedChanged(bool value) => OnPropertyChanged(nameof(ShowPreview));

    partial void OnPreviewChanged(PreviewViewModel? value) => OnPropertyChanged(nameof(ShowPreview));
    [ObservableProperty] public partial InventoryViewModel? Inventory { get; set; }
    [ObservableProperty] public partial VehiclesViewModel? Vehicles { get; set; }
    [ObservableProperty] public partial SaveInfoViewModel? Info { get; set; }
    [ObservableProperty] public partial NodesViewModel? Nodes { get; set; }

    public AboutViewModel About { get; } = new();

    public string WindowTitle => (FilePath is null ? "RelicWriter" : $"RelicWriter - {FilePath}") + (IsDirty ? " *" : "");

    partial void OnFilePathChanged(string? value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    public string GameDataText => GameDatabase.Current.Info.Describe;

    public bool HasGameIcons => GameIcons.Current is not null;

    public string VersionText => "v" + AppVersion.DisplayText;

    const int MaxRecentSaves = 12;
    const string IssuesUrl = "https://github.com/hendy643/RelicWriter/issues";

    public MainViewModel()
    {
        // Vehicle pictures are read from the player's own game files; without them the tiles show a drawn glyph.
        GameIcons.Current ??= GameLocator.Find() is { } dir ? GameIcons.Open(dir) : null;
        foreach (var save in SaveLocator.FindSaves().Take(MaxRecentSaves)) RecentSaves.Add(save);
    }

    [RelayCommand]
    async Task OpenAsync()
    {
        if (Dialogs is null) return;
        if (await Dialogs.PickOpenAsync() is { } path) Load(path);
    }

    [RelayCommand]
    async Task LocateGameAsync()
    {
        if (Dialogs is null || await Dialogs.PickFolderAsync() is not { } dir) return;
        if (!GameLocator.IsGameFolder(dir) || GameIcons.Open(dir) is not { } icons)
        {
            SetStatus("That folder is not a readable Cyberpunk 2077 install.", true);
            return;
        }
        GameLocator.Remember(dir);
        GameIcons.Current = icons;
        OnPropertyChanged(nameof(HasGameIcons));
        if (IsLoaded && !IsDirty) Load(FilePath!);
        else SetStatus("Game found. Vehicle pictures appear the next time a save is opened.", false);
    }

    /// <summary>Opens the issue tracker and shows a bug report filled in from the issue template (versions and counts only, see <see cref="BugReport"/>) to copy into the new issue. Nothing is posted.</summary>
    [RelayCommand]
    async Task ReportBugAsync()
    {
        if (Dialogs is null) return;
        await Dialogs.OpenUrlAsync(IssuesUrl);
        await Dialogs.ShowBugReportAsync(BugReport.Build(new(file, file is null ? null : Info?.PhantomLiberty, inventory, Inventory?.Selected?.Entry, BackupsBesideSave())));
    }

    [RelayCommand]
    void OpenRecent(RecentSave save) => Load(save.Path);

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    void Save() => Write(FilePath!);

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    async Task SaveAsAsync()
    {
        if (Dialogs is null) return;
        if (await Dialogs.PickSaveAsync("sav.dat") is { } path) Write(path);
    }

    public void Load(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var loaded = SaveFile.Read(bytes);
            var dev = PlayerDevelopment.Read(loaded);
            var inv = RelicWriter.Common.Model.Inventory.Read(loaded);
            var gar = VehicleGarage.Read(loaded);
            var app = Common.Model.Appearance.Read(loaded);
            file = loaded;
            appearance = app;
            development = dev;
            inventory = inv;
            garage = gar;
            FilePath = path;
            var expansion = dev is not null && GameEdition.LooksLikePhantomLiberty(dev);
            var edition = new EditionViewModel(new GameEdition(loaded.Header.GameVersion, expansion));
            Character = dev is null ? null : new CharacterViewModel(dev, MarkDirty, edition);
            RebuildPerks(dev, edition.Edition);
            edition.PropertyChanged += (_, _) => RebuildPerks(dev, edition.Edition);
            Appearance = app is null ? null : new AppearanceViewModel(app, MarkDirty);
            Preview = Appearance is null ? null : new PreviewViewModel(Appearance);
            Inventory = inv is null ? null : new InventoryViewModel(inv, MarkDirty, edition);
            Vehicles = new VehiclesViewModel(gar, MarkDirty);
            Info = new SaveInfoViewModel(loaded, path, bytes.Length, edition, expansion, MarkDirty);
            Nodes = new NodesViewModel(loaded);
            IsLoaded = true;
            IsDirty = false;
            SetStatus($"Loaded {Path.GetFileName(Path.GetDirectoryName(path))} - game {loaded.Header.GameVersionLabel}, save version {loaded.Header.SaveVersion}." +
                (GameDatabase.Current.IsOlderThanSave(loaded.Header.SaveVersion) ? " This save is newer than the game data, so some names may be missing." : ""), false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            SetStatus($"Could not open {path}: {e.Message}", true);
        }
    }

    // The perk trees are the 2.x ones, so older saves have no Perks tab; the Relic tree is in only with the expansion.
    void RebuildPerks(PlayerDevelopment? dev, GameEdition edition) =>
        Perks = dev is null || Character is null || !edition.HasPerkTrees ? null : new PerksViewModel(dev, Character, MarkDirty, edition);

    void Write(string path)
    {
        if (file is null) return;
        try
        {
            development?.ApplyTo(file);
            inventory?.ApplyTo();
            garage?.ApplyTo();
            appearance?.ApplyTo();
            var bytes = file.Write();

            // Keep the file the game wrote: one backup per path per session, before the first overwrite.
            if (File.Exists(path) && backedUp.Add(path))
                File.Copy(path, $"{path}.relicwriter-backup-{DateTime.Now:yyyyMMdd-HHmmss}");
            var temp = path + ".relicwriter-tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);

            FilePath = path;
            IsDirty = false;
            SetStatus($"Saved {path}. A backup of the original was kept next to it.", false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus($"Could not save: {e.Message}", true);
        }
    }

    // RelicWriter keeps a backup next to a save the first time it overwrites it, so any backup there means this file was written by RelicWriter before.
    int? BackupsBesideSave() =>
        FilePath is { } path && Path.GetDirectoryName(path) is { } dir && Directory.Exists(dir) ? Directory.GetFiles(dir, Path.GetFileName(path) + ".relicwriter-backup-*").Length : null;

    void MarkDirty() => IsDirty = true;

    void SetStatus(string text, bool error)
    {
        Status = text;
        StatusIsError = error;
    }
}

/// <summary>Finds the game's save folders.</summary>
public static class SaveLocator
{
    const string SavedGames = "Saved Games";

    public static IEnumerable<RecentSave> FindSaves()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new List<string> { Path.Combine(home, SavedGames, "CD Projekt Red", "Cyberpunk 2077") };
        // On Linux the saves are inside a Wine prefix, under a user name that differs between launchers (steamuser, the login name).
        if (!OperatingSystem.IsWindows())
            foreach (var prefix in GameLocator.LinuxWinePrefixes().Distinct())
            {
                var users = Path.Combine(prefix, "drive_c", "users");
                if (Directory.Exists(users))
                    roots.AddRange(Directory.GetDirectories(users).Select(u => Path.Combine(u, SavedGames, "CD Projekt Red", "Cyberpunk 2077")));
            }
        return roots.Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Select(d => Path.Combine(d, "sav.dat"))
            .Where(File.Exists)
            .Select(p => new RecentSave(Path.GetFileName(Path.GetDirectoryName(p))!, p, File.GetLastWriteTime(p)))
            .OrderByDescending(s => s.Modified);
    }
}
