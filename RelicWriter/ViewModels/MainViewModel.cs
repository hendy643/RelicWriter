using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicWriter.Common.Container;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;
using RelicWriter.Common.Updates;
using RelicWriter.GameAssets;

namespace RelicWriter.ViewModels;

/// <summary>Asks the user for a file; implemented by the window so the view model stays free of UI types.</summary>
public interface IFileDialogs
{
    Task<string?> PickOpenAsync();

    Task<string?> PickFolderAsync();

    Task<string?> PickSaveAsync(string suggestedFileName);
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

    public string WindowTitle => (FilePath is null ? "RelicWriter" : $"RelicWriter - {FilePath}") + (IsDirty ? " *" : "");

    partial void OnFilePathChanged(string? value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    public string GameDataText => GameDatabase.Current.Info.Describe;

    public bool HasGameIcons => GameIcons.Current is not null;

    public string VersionText => "v" + AppVersion.Text;

    /// <summary>Closes the app; set by the window. Used to let an installer replace the app's files.</summary>
    public Action? Exit { get; set; }

    // Set to skip the update check, for example when running from a build folder.
    const string NoUpdateVariable = "RELICWRITER_NO_UPDATE";
    static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15), DownloadTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// In the background, looks for a newer release and downloads its installer. With no save open it then runs the installer and closes the app;
    /// otherwise it says the update is ready, and the next start installs it, so unsaved edits are never lost. Any failure (offline, no release,
    /// damaged download) is ignored: the app carries on as it is.
    /// </summary>
    public async void StartUpdateCheck()
    {
        if (Debugger.IsAttached || Environment.GetEnvironmentVariable(NoUpdateVariable) is not null) return;
        try
        {
            using var http = new HttpClient();
            var checker = new UpdateChecker(http);
            using var check = new CancellationTokenSource(CheckTimeout);
            if (await checker.CheckAsync(AppVersion.Current, check.Token) is not { } update) return;
            if (update.Installer is not { } installer)
            {
                Dispatcher.UIThread.Post(() => SetStatus($"RelicWriter {update.Version} is available: {update.PageUrl}", false));
                return;
            }
            Dispatcher.UIThread.Post(() => SetStatus($"Downloading RelicWriter {update.Version}…", false));
            using var download = new CancellationTokenSource(DownloadTimeout);
            var file = await checker.DownloadAsync(installer, download.Token);
            Dispatcher.UIThread.Post(() =>
            {
                if (IsLoaded) { SetStatus($"RelicWriter {update.Version} is ready. It installs the next time the app starts.", false); return; }
                UpdateChecker.Launch(file);
                Exit?.Invoke();
            });
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or InvalidDataException
                                      or InvalidOperationException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException)
        {
            // The update is optional; try again at the next start.
        }
    }

    const int MaxRecentSaves = 12;

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
            SetStatus("That folder is not a readable Cyberpunk 2077 install (vehicle pictures can only be read on Windows).", true);
            return;
        }
        GameLocator.Remember(dir);
        GameIcons.Current = icons;
        OnPropertyChanged(nameof(HasGameIcons));
        if (IsLoaded && !IsDirty) Load(FilePath!);
        else SetStatus("Game found. Vehicle pictures appear the next time a save is opened.", false);
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
            Character = dev is null ? null : new CharacterViewModel(dev, MarkDirty);
            Perks = dev is null || Character is null ? null : new PerksViewModel(dev, Character, MarkDirty);
            Appearance = app is null ? null : new AppearanceViewModel(app, MarkDirty);
            Preview = Appearance is null ? null : new PreviewViewModel(Appearance);
            Inventory = inv is null ? null : new InventoryViewModel(inv, MarkDirty);
            Vehicles = new VehiclesViewModel(gar, MarkDirty);
            Info = new SaveInfoViewModel(loaded, path, bytes.Length);
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
    public static IEnumerable<RecentSave> FindSaves()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots =
        [
            Path.Combine(home, "Saved Games", "CD Projekt Red", "Cyberpunk 2077"),
            // Steam with Proton on Linux
            Path.Combine(home, ".local", "share", "Steam", "steamapps", "compatdata", "1091500", "pfx", "drive_c", "users", "steamuser", "Saved Games", "CD Projekt Red", "Cyberpunk 2077"),
        ];
        return roots.Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Select(d => Path.Combine(d, "sav.dat"))
            .Where(File.Exists)
            .Select(p => new RecentSave(Path.GetFileName(Path.GetDirectoryName(p))!, p, File.GetLastWriteTime(p)))
            .OrderByDescending(s => s.Modified);
    }
}
