using System.Text.Json;
using System.Text.RegularExpressions;

namespace RelicWriter.Common.GameData;

/// <summary>
/// Finds the Cyberpunk 2077 install. In order: the RELICWRITER_GAME_DIR variable, a remembered folder, then the config files of the launchers
/// (Steam, GOG Galaxy, Epic on Windows; Steam, Lutris, Heroic on Linux). Every candidate is checked for r6/cache/tweakdb.bin.
/// </summary>
/// <remarks>Checked against the file formats only, on one Windows Steam install; the Linux, GOG and Epic locations are untested on real installs.</remarks>
public static partial class GameLocator
{
    public const string SteamAppId = "1091500", GogGameId = "1423049311", EpicAppName = "Ginger", FolderName = "Cyberpunk 2077";
    const string ExeName = "Cyberpunk2077.exe", Slug = "cyberpunk";
    /// <summary>The exe is in bin/x64, so the game folder is this many levels above it.</summary>
    const int ExeDepth = 3;


    public static bool IsGameFolder(string? dir) => !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "r6", "cache", "tweakdb.bin"));

    public static string? Find()
    {
        var env = Environment.GetEnvironmentVariable("RELICWRITER_GAME_DIR");
        if (IsGameFolder(env)) return env;
        if (LocalData.Get(LocalData.GameFolder) is { } saved && IsGameFolder(saved)) return saved;

        return Candidates().Where(c => !string.IsNullOrWhiteSpace(c)).FirstOrDefault(IsGameFolder);
    }

    /// <summary>Remembers a folder the user picked, for the next start.</summary>
    public static void Remember(string dir) => LocalData.Set(LocalData.GameFolder, dir);

    static IEnumerable<string> Candidates()
    {
        foreach (var c in OperatingSystem.IsWindows() ? WindowsCandidates() : LinuxCandidates()) yield return c;
    }

    // ---- Windows ----

    static IEnumerable<string> WindowsCandidates()
    {
        var roots = new List<string>();
        foreach (var key in new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam" })
            if (Microsoft.Win32.Registry.GetValue(key, "InstallPath", null) is string p) roots.Add(p);
        if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) is string user) roots.Add(user);
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (var c in SteamCandidates(roots)) yield return c;

        // GOG Galaxy writes the game's folder to the registry.
        foreach (var view in new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games\", @"SOFTWARE\GOG.com\Games\" })
            if (Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\" + view + GogGameId, "path", null) is string gog) yield return gog;

        // Epic keeps one JSON manifest per installed game.
        var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (Directory.Exists(manifests))
            foreach (var file in Directory.EnumerateFiles(manifests, "*.item"))
                foreach (var c in EpicPaths(SafeRead(file))) yield return c;
    }

    // ---- Linux ----

    static IEnumerable<string> LinuxCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var share = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".local", "share");
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } y ? y : Path.Combine(home, ".config");
        var flatpak = Path.Combine(home, ".var", "app");

        foreach (var c in SteamCandidates([Path.Combine(share, "Steam"), Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".steam", "root"),
                     Path.Combine(flatpak, "com.valvesoftware.Steam", ".local", "share", "Steam")])) yield return c;

        // Lutris: one YAML per game, naming the exe and the Wine prefix.
        foreach (var dir in new[] { Path.Combine(config, "lutris", "games"), Path.Combine(flatpak, "net.lutris.Lutris", "config", "lutris", "games") })
            if (Directory.Exists(dir))
                foreach (var file in Directory.EnumerateFiles(dir, "*.yml"))
                    foreach (var c in LutrisPaths(SafeRead(file))) yield return c;

        // Heroic: GOG and Epic installs are listed in JSON files in its config folder.
        foreach (var heroic in new[] { Path.Combine(config, "heroic"), Path.Combine(flatpak, "com.heroicgameslauncher.hgl", "config", "heroic") })
        {
            foreach (var c in HeroicPaths(SafeRead(Path.Combine(heroic, "gog_store", "installed.json")))) yield return c;
            foreach (var c in HeroicPaths(SafeRead(Path.Combine(heroic, "legendaryConfig", "legendary", "installed.json")))) yield return c;
            yield return Path.Combine(home, "Games", "Heroic", FolderName);
        }
    }

    // ---- Steam (both systems) ----

    static IEnumerable<string> SteamCandidates(IEnumerable<string> roots)
    {
        foreach (var root in roots.Where(Directory.Exists))
        {
            var libraries = new List<string> { root };
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf)) libraries.AddRange(SteamLibraries(SafeRead(vdf)));
            foreach (var library in libraries)
            {
                var steamapps = Path.Combine(library, "steamapps");
                // The app manifest names the install folder; the default name is the fallback.
                var installDir = SteamInstallDir(SafeRead(Path.Combine(steamapps, $"appmanifest_{SteamAppId}.acf"))) ?? FolderName;
                yield return Path.Combine(steamapps, "common", installDir);
            }
        }
    }

    /// <summary>The library folders listed in Steam's libraryfolders.vdf.</summary>
    internal static IEnumerable<string> SteamLibraries(string vdf) =>
        LibraryPath().Matches(vdf).Select(m => m.Groups[1].Value.Replace(@"\\", @"\"));

    /// <summary>The "installdir" of a Steam appmanifest_*.acf, or null.</summary>
    internal static string? SteamInstallDir(string acf) => InstallDir().Match(acf) is { Success: true } m ? m.Groups[1].Value : null;

    // ---- Lutris, Heroic, Epic ----

    /// <summary>Candidate game folders from a Lutris game YAML: the exe's install folder, and the usual GOG folders in its Wine prefix. Only Cyberpunk entries give any.</summary>
    internal static IEnumerable<string> LutrisPaths(string yaml)
    {
        if (!yaml.Contains(Slug, StringComparison.OrdinalIgnoreCase)) yield break;
        if (YamlValue("exe").Match(yaml) is { Success: true } exe)
            yield return FromExe(exe.Groups[1].Value);
        if (YamlValue("prefix").Match(yaml) is { Success: true } prefix)
            foreach (var gog in new[] { Path.Combine("GOG Games", FolderName), Path.Combine("Program Files (x86)", "GOG Galaxy", "Games", FolderName), Path.Combine("Program Files", "Epic Games", FolderName) })
                yield return Path.Combine(prefix.Groups[1].Value, "drive_c", gog);
    }

    /// <summary>Install folders of Cyberpunk entries in a Heroic installed.json (GOG: <c>{"installed":[{appName, install_path}]}</c>; Epic/legendary: <c>{appName: {install_path, title}}</c>).</summary>
    internal static IEnumerable<string> HeroicPaths(string json)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement; }
        catch (JsonException) { yield break; }
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("installed", out var list)) root = list;
        var entries = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray().Select(e => (Key: e.TryGetProperty("appName", out var n) ? n.GetString() : null, Entry: e)),
            JsonValueKind.Object => root.EnumerateObject().Select(p => (Key: (string?)p.Name, Entry: p.Value)),
            _ => [],
        };
        foreach (var (key, entry) in entries)
            if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("install_path", out var path) && path.GetString() is { } dir &&
                (key == GogGameId || key == EpicAppName || dir.Contains(FolderName, StringComparison.OrdinalIgnoreCase)))
                yield return dir;
    }

    /// <summary>The InstallLocation of an Epic manifest (.item) for Cyberpunk 2077.</summary>
    internal static IEnumerable<string> EpicPaths(string json)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement; }
        catch (JsonException) { yield break; }
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("InstallLocation", out var dir) && dir.GetString() is { } location &&
            root.TryGetProperty("DisplayName", out var name) && name.GetString() == FolderName)
            yield return location;
    }

    /// <summary>The game folder for the path of its exe (bin/x64/Cyberpunk2077.exe), or the path itself when it isn't one.</summary>
    static string FromExe(string exe)
    {
        if (!exe.EndsWith(ExeName, StringComparison.OrdinalIgnoreCase)) return exe;
        // Cut by hand: Lutris on Linux can name a Windows-style path, and Path would only split on this system's separator.
        var dir = exe;
        for (var i = 0; i < ExeDepth; i++)
            dir = dir[..Math.Max(0, Math.Max(dir.LastIndexOf('/'), dir.LastIndexOf('\\')))];
        return dir;
    }

    static string SafeRead(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPath();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"")]
    private static partial Regex InstallDir();

    static Regex YamlValue(string key) => new($@"^\s*{key}:\s*['""]?(.+?)['""]?\s*$", RegexOptions.Multiline);
}
