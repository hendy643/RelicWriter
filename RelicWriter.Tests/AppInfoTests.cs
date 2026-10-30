using RelicWriter.Common.AppInfo;
using RelicWriter.Common.Model;
using RelicWriter.Tests.Fixtures;
using Xunit;

namespace RelicWriter.Tests;

public class AppInfoTests
{
    [Fact]
    public void TheChangelogIsMadeFromTheFileAtBuildTime()
    {
        var file = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CHANGELOG.md"));
        Assert.Equal(file.Count(l => l.StartsWith("## ")), ChangelogData.Entries.Count);
        Assert.Equal(file.Count(l => l.StartsWith("- ")), ChangelogData.Entries.Sum(e => e.Lines.Count));
        Assert.All(ChangelogData.Entries, e => Assert.NotEmpty(e.Lines));
    }

    [Fact]
    public void TheShownVersionCarriesTheCommitHashOnlyForBuildsThatAreNotReleases()
    {
        Assert.Equal(AppVersion.Revision is null ? AppVersion.Text : AppVersion.Text + "+" + AppVersion.Revision, AppVersion.DisplayText);
    }

    [Fact]
    public void TheBugReportListsWhatTheEditorAddedAndTheSelectedItemsModTree()
    {
        var save = SyntheticSave.Read(277);
        foreach (var (node, i) in save.Walk().Select((n, i) => (n, i))) node.Id = (uint)i;
        var inv = Inventory.Read(save)!;
        var added = inv.Add(0xABCDEF01, 20, inv.SubInventories[0].Items[1].Shape, 5)!;
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonIn(BugReport.Build(new(save, null, inv, added, 2))))!["save"]!;
        Assert.Equal("add", json["inventory"]!["editsThisSession"]![0]!["action"]!.GetValue<string>());
        Assert.Equal(2, json["relicWriterBackupsBesideSave"]!.GetValue<int>());
        Assert.Equal(inv.SubInventories[0].Items[^1].NodeBytes, json["selectedItem"]!["nodeBytes"]!.GetValue<int>());
    }

    static string JsonIn(string report) => report.Split("```json")[1].Split("```")[0];

    [Fact]
    public void TheBugReportHasVersionsAndCountsAndNothingThatIdentifiesAnyone()
    {
        var save = SyntheticSave.Read(277);
        var text = BugReport.Build(new(save, true));
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonIn(text))!;
        Assert.Equal(277u, json["save"]!["saveVersion"]!.GetValue<uint>());
        Assert.Equal(3, json["save"]!["inventory"]!["items"]!.GetValue<int>());
        Assert.NotNull(json["gameData"]!["gameVersion"]);
        Assert.Null(System.Text.Json.Nodes.JsonNode.Parse(JsonIn(BugReport.Build()))!["save"]);

        // Nothing of the machine or the player: no user or machine name, no folder, no clock or date.
        foreach (var secret in new[] { Environment.UserName, Environment.MachineName, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppContext.BaseDirectory })
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("imestamp", text);
        Assert.Contains("**Describe the bug**", text);
        Assert.DoesNotContain("path", text, StringComparison.OrdinalIgnoreCase);
    }
}
