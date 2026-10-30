using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using RelicWriter.Common.Container;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;

namespace RelicWriter.Common.AppInfo;

/// <summary>
/// The text of a bug report: versions and counts as JSON, for attaching to an issue. It holds nothing that identifies the player or their machine: no file
/// or folder paths, no save time, no names, no machine or user name, no ids from the save, no values the player entered. Only numbers, flags and names the
/// app and the save format define (the node names in <see cref="NodeAvailability"/>) are written; keep it that way when adding to it.
/// </summary>
public static class BugReport
{
    const string GameExe = "Cyberpunk2077.exe";

    public static string Build(SaveFile? save = null, bool? phantomLiberty = null)
    {
        var data = GameDatabase.Current.Info;
        var report = new JsonObject
        {
            ["app"] = new JsonObject { ["version"] = AppVersion.Text, ["revision"] = AppVersion.Revision, ["databaseSaveVersion"] = AppVersion.DatabaseVersion },
            ["gameData"] = new JsonObject { ["gameVersion"] = data.GameVersion, ["dataVersion"] = data.DataVersion, ["maxSaveVersion"] = data.MaxSaveVersion },
            ["system"] = new JsonObject { ["os"] = RuntimeInformation.OSDescription, ["architecture"] = RuntimeInformation.OSArchitecture.ToString(), ["runtime"] = RuntimeInformation.FrameworkDescription },
            ["installedGame"] = InstalledGame(),
        };
        if (save is not null) report["save"] = Describe(save, phantomLiberty);
        return report.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    // Whether the game was found and its exe's product version, never where it is.
    static JsonObject InstalledGame()
    {
        string? version = null;
        var dir = GameLocator.Find();
        if (dir is not null)
            try { version = FileVersionInfo.GetVersionInfo(Path.Combine(dir, "bin", "x64", GameExe)).ProductVersion; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return new JsonObject { ["found"] = dir is not null, ["productVersion"] = version };
    }

    static JsonObject Describe(SaveFile save, bool? phantomLiberty)
    {
        var h = save.Header;
        var nodes = save.Walk().Select(n => n.Name).ToList();
        var known = new JsonObject();
        foreach (var g in nodes.Where(NodeAvailability.Nodes.ContainsKey).GroupBy(n => n).OrderBy(g => g.Key, StringComparer.Ordinal)) known[g.Key] = g.Count();
        var inventory = Inventory.Read(save);
        return new JsonObject
        {
            ["saveVersion"] = h.SaveVersion,
            ["gameVersion"] = h.GameVersion,
            ["gameVersionLabel"] = h.GameVersionLabel,
            ["archiveVersion"] = h.ArchiveVersion,
            ["phantomLiberty"] = phantomLiberty,
            ["nodeCount"] = nodes.Count,
            ["unrecognisedNodeCount"] = nodes.Count(n => !NodeAvailability.Nodes.ContainsKey(n)),
            ["nodes"] = known,
            ["inventory"] = inventory is null ? null : new JsonObject { ["containers"] = inventory.SubInventories.Count, ["items"] = inventory.SubInventories.Sum(s => s.Items.Count) },
        };
    }
}
