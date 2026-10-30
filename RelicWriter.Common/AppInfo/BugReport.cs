using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using RelicWriter.Common.Container;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;

namespace RelicWriter.Common.AppInfo;

/// <summary>
/// The text of a bug report for a new GitHub issue: the sections of .github/ISSUE_TEMPLATE/bug_report.md, with the desktop details filled in and versions and counts as JSON under additional context. It holds nothing that identifies the player or their machine: no file
/// or folder paths, no save time, no names, no machine or user name, no ids from the save, no values the player entered. Only numbers, flags and names the
/// app and the save format define (the node names in <see cref="NodeAvailability"/>) are written; keep it that way when adding to it.
/// </summary>
/// <summary>What the app knows when the report is made. Everything is optional: a report can be made with no save open.</summary>
/// <param name="BackupsBesideSave">How many backups RelicWriter made beside the open save; any means RelicWriter wrote that file before.</param>
public sealed record BugReportContext(SaveFile? Save = null, bool? PhantomLiberty = null, Inventory? Inventory = null, ItemEntry? SelectedItem = null, int? BackupsBesideSave = null);

public static class BugReport
{
    const string GameExe = "Cyberpunk2077.exe";
    const int MaxListed = 50;
    // Folders where mods put files, relative to the game folder. Only how many files each holds is reported.
    static readonly string[] ModFolders = ["archive/pc/mod", "r6/scripts", "r6/tweaks", "red4ext/plugins", "bin/x64/plugins"];

    /// <summary>The issue text. The sections the user has to write are left as the template has them; the template's smartphone and browser parts do not apply to a desktop app and are left out.</summary>
    public static string Build(BugReportContext? context = null) => $"""
        **Describe the bug**
        A clear and concise description of what the bug is.

        **To Reproduce**
        Steps to reproduce the behavior:
        1. Go to '...'
        2. Click on '....'
        3. Scroll down to '....'
        4. See error

        **Expected behavior**
        A clear and concise description of what you expected to happen.

        **Screenshots**
        If applicable, add screenshots to help explain your problem.

        **Desktop (please complete the following information):**
         - OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})
         - Version: {AppVersion.DisplayText}

        **Additional context**
        Add any other context about the problem here.

        <details>
        <summary>Details from RelicWriter</summary>

        ```json
        {Details(context ?? new())}
        ```
        </details>
        """.ReplaceLineEndings("\n");

    static string Details(BugReportContext c)
    {
        var data = GameDatabase.Current.Info;
        var report = new JsonObject
        {
            ["app"] = new JsonObject { ["version"] = AppVersion.Text, ["revision"] = AppVersion.Revision, ["databaseSaveVersion"] = AppVersion.DatabaseVersion },
            ["gameData"] = new JsonObject { ["gameVersion"] = data.GameVersion, ["dataVersion"] = data.DataVersion, ["maxSaveVersion"] = data.MaxSaveVersion },
            ["system"] = new JsonObject { ["os"] = RuntimeInformation.OSDescription, ["architecture"] = RuntimeInformation.OSArchitecture.ToString(), ["runtime"] = RuntimeInformation.FrameworkDescription },
            ["installedGame"] = InstalledGame(),
        };
        if (c.Save is not null) report["save"] = Describe(c);
        return report.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    // Whether the game was found, its exe's product version and how many files the usual mod folders hold; never where it is or what the files are called.
    static JsonObject InstalledGame()
    {
        string? version = null;
        var mods = new JsonObject();
        var dir = GameLocator.Find();
        if (dir is not null)
        {
            try { version = FileVersionInfo.GetVersionInfo(Path.Combine(dir, "bin", "x64", GameExe)).ProductVersion; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            foreach (var folder in ModFolders) mods[folder] = FileCount(Path.Combine(dir, folder));
        }
        return new JsonObject { ["found"] = dir is not null, ["productVersion"] = version, ["modFileCounts"] = mods };
    }

    // Null when the folder is not there.
    static int? FileCount(string folder)
    {
        try { return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count() : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    // A record's name where the game data has it, else its id (a hash of the name, so it names nobody).
    static string Label(uint hash, byte length) => RecordNames.Record(hash, length) ?? $"{hash:x8}/{length}";

    static JsonObject Describe(BugReportContext c)
    {
        var save = c.Save!;
        var h = save.Header;
        var nodes = save.Walk().Select(n => n.Name).ToList();
        var known = new JsonObject();
        foreach (var g in nodes.Where(NodeAvailability.Nodes.ContainsKey).GroupBy(n => n).OrderBy(g => g.Key, StringComparer.Ordinal)) known[g.Key] = g.Count();
        var inventory = c.Inventory ?? Inventory.Read(save);
        return new JsonObject
        {
            ["saveVersion"] = h.SaveVersion,
            ["gameVersion"] = h.GameVersion,
            ["gameVersionLabel"] = h.GameVersionLabel,
            ["archiveVersion"] = h.ArchiveVersion,
            ["phantomLiberty"] = c.PhantomLiberty,
            ["relicWriterBackupsBesideSave"] = c.BackupsBesideSave,
            ["nodeCount"] = nodes.Count,
            ["unrecognisedNodeCount"] = nodes.Count(n => !NodeAvailability.Nodes.ContainsKey(n)),
            ["nodes"] = known,
            ["inventory"] = inventory is null ? null : DescribeInventory(inventory),
            ["selectedItem"] = inventory is null || c.SelectedItem is null ? null : DescribeItem(inventory, c.SelectedItem),
        };
    }

    static JsonObject DescribeInventory(Inventory inventory)
    {
        var items = inventory.SubInventories.SelectMany(s => s.Items).ToList();
        var weapons = items.Where(i => GameDatabase.Current.Item(i.RecordHash, i.RecordLength)?.Category == "Weapon").ToList();
        var sizes = new JsonObject();
        foreach (var g in weapons.GroupBy(i => i.NodeBytes).OrderBy(g => g.Key)) sizes[g.Key.ToString()] = g.Count();
        var edits = new JsonArray();
        foreach (var e in inventory.Edits.TakeLast(MaxListed))
            edits.Add(new JsonObject
            {
                ["action"] = e.Action, ["record"] = Label(e.Hash, e.Length), ["shape"] = e.Shape.ToString(), ["nodeBytes"] = e.Bytes,
                ["copiedFrom"] = e.DonorHash is { } dh ? Label(dh, e.DonorLength!.Value) : null, ["copiedFromNodeBytes"] = e.DonorBytes,
            });
        return new JsonObject
        {
            ["containers"] = inventory.SubInventories.Count,
            ["items"] = items.Count,
            // Weapons the game makes carry attachment slots; one whose tree is only the root is what the editor builds when it adds one.
            ["weapons"] = new JsonObject { ["count"] = weapons.Count, ["withoutMods"] = weapons.Count(i => inventory.ModTree(i)?.Mods == 0), ["unreadableModTree"] = weapons.Count(i => inventory.ModTree(i) is null), ["nodeBytes"] = sizes },
            ["editsThisSession"] = edits,
        };
    }

    static JsonObject DescribeItem(Inventory inventory, ItemEntry item)
    {
        var tree = inventory.ModTree(item);
        return new JsonObject
        {
            ["record"] = Label(item.RecordHash, item.RecordLength),
            ["category"] = GameDatabase.Current.Item(item.RecordHash, item.RecordLength)?.Category,
            ["shape"] = item.Shape.ToString(),
            ["nodeBytes"] = item.NodeBytes,
            ["modTree"] = tree is null ? null : new JsonObject
            {
                ["root"] = Label(tree.RootHash, tree.RootLength),
                ["mods"] = tree.Mods,
                ["depth"] = tree.Depth,
                ["modRecords"] = new JsonArray(tree.ModRecords.Take(MaxListed).Select(r => (JsonNode?)Label(r.Hash, r.Length)).ToArray()),
                ["slotRecords"] = new JsonArray(tree.SlotRecords.Take(MaxListed).Select(r => (JsonNode?)Label(r.Hash, r.Length)).ToArray()),
            },
        };
    }
}
