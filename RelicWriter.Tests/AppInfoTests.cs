using RelicWriter.Common.AppInfo;
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
}
