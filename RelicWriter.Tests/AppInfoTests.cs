using RelicWriter.Common.AppInfo;
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
    public void TheBugReportHasVersionsAndCountsAndNothingThatIdentifiesAnyone()
    {
        var save = SyntheticSave.Read(277);
        var text = BugReport.Build(save, true);
        var json = System.Text.Json.Nodes.JsonNode.Parse(text)!;
        Assert.Equal(277u, json["save"]!["saveVersion"]!.GetValue<uint>());
        Assert.Equal(3, json["save"]!["inventory"]!["items"]!.GetValue<int>());
        Assert.NotNull(json["gameData"]!["gameVersion"]);
        Assert.Null(System.Text.Json.Nodes.JsonNode.Parse(BugReport.Build())!["save"]);

        // Nothing of the machine or the player: no user or machine name, no folder, no clock or date.
        foreach (var secret in new[] { Environment.UserName, Environment.MachineName, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppContext.BaseDirectory })
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("imestamp", text);
        Assert.DoesNotContain("path", text, StringComparison.OrdinalIgnoreCase);
    }
}
