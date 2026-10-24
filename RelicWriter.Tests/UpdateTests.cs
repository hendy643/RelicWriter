using System.Net;
using RelicWriter.Common.Updates;
using Xunit;

namespace RelicWriter.Tests;

/// <summary>The update check, against a stand-in for GitHub. Nothing here uses the network.</summary>
public class UpdateTests
{
    const string Release = """
        {"html_url":"https://github.com/o/r/releases/tag/v1.2.0","assets":[
          {"name":"version.json","browser_download_url":"https://dl/version.json","size":20},
          {"name":"RelicWriter-1.2.0-linux.tar.gz","browser_download_url":"https://dl/linux","size":5},
          {"name":"RelicWriter-Setup-1.2.0.exe","browser_download_url":"https://dl/exe","size":9,"digest":"sha256:ABCD"},
          {"name":"RelicWriter-1.2.0.msi","browser_download_url":"https://dl/msi","size":7}]}
        """;

    sealed class Stub(Dictionary<string, string> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) =>
            Task.FromResult(responses.TryGetValue(request.RequestUri!.ToString(), out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    static UpdateChecker Checker(string release, string? versionFile) => new(new HttpClient(new Stub(new()
    {
        ["https://api.github.com/repos/hendy643/RelicWriter/releases/latest"] = release,
        ["https://dl/version.json"] = versionFile ?? "",
    })));

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("1.2.3-beta.1+build5", "1.2.3-beta.1")]
    public void VersionsParse(string text, string expected) => Assert.Equal(expected, SemVer.Parse(text)!.ToString());

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1.2.3-")]
    [InlineData(null)]
    public void NonsenseIsNotAVersion(string? text) => Assert.Null(SemVer.Parse(text));

    [Fact]
    public void TheVersionFileNeedsAnAppVersionString()
    {
        Assert.Equal(SemVer.Parse("1.2.0"), UpdateChecker.ParseVersionFile("""{"app":"1.2.0"}"""));
        Assert.Null(UpdateChecker.ParseVersionFile("""{"app":3}"""));
        Assert.Null(UpdateChecker.ParseVersionFile("not json"));
    }

    [Fact]
    public void TheInstallerIsPickedByExtensionInOrderOfPreference()
    {
        var (page, assets) = UpdateChecker.ParseRelease(Release);
        Assert.Equal("https://github.com/o/r/releases/tag/v1.2.0", page);
        Assert.Equal("RelicWriter-1.2.0.msi", UpdateChecker.PickInstaller(assets, [".msi", ".exe"])!.Name);
        var exe = UpdateChecker.PickInstaller(assets, [".exe"])!;
        Assert.Equal("ABCD", exe.Sha256);
        Assert.Null(UpdateChecker.PickInstaller(assets, []));
        Assert.Null(UpdateChecker.PickInstaller(assets, [".json"]));
    }

    [Fact]
    public async Task ANewerReleaseWithAVersionFileIsAnUpdate()
    {
        var update = await Checker(Release, """{"app":"1.2.0"}""").CheckAsync(SemVer.Parse("1.1.0")!, default);
        Assert.Equal(SemVer.Parse("1.2.0"), update!.Version);
    }

    [Fact]
    public async Task NoUpdateWhenTheVersionIsNotNewerOrThereIsNoVersionFile()
    {
        Assert.Null(await Checker(Release, """{"app":"1.2.0"}""").CheckAsync(SemVer.Parse("1.2.0")!, default));
        Assert.Null(await Checker(Release, """{"app":"1.2.0"}""").CheckAsync(SemVer.Parse("2.0.0")!, default));
        Assert.Null(await Checker(Release.Replace("version.json", "other.json"), null).CheckAsync(SemVer.Parse("1.0.0")!, default));
        Assert.Null(await Checker("""{"assets":[]}""", null).CheckAsync(SemVer.Parse("1.0.0")!, default));
    }

    [Fact]
    public void PreReleasesAreLowerThanTheReleaseTheyLeadUpTo()
    {
        // From semver.org: 1.0.0-alpha < 1.0.0-alpha.1 < 1.0.0-alpha.beta < 1.0.0-beta < 1.0.0-beta.2 < 1.0.0-beta.11 < 1.0.0-rc.1 < 1.0.0
        string[] ascending = ["1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0", "1.0.1-alpha", "1.1.0"];
        var versions = ascending.Select(v => SemVer.Parse(v)!).ToList();
        for (var i = 0; i < versions.Count; i++)
            for (var j = 0; j < versions.Count; j++)
                Assert.Equal(i.CompareTo(j), Math.Sign(versions[i].CompareTo(versions[j])));
        Assert.True(SemVer.Parse("1.2.0-beta")! < SemVer.Parse("1.2.0")!);
        Assert.Equal(0, SemVer.Parse("1.2.0+build.1")!.CompareTo(SemVer.Parse("1.2.0")));
    }

    [Fact]
    public async Task AReleaseCandidateIsNotAnUpdateToTheReleaseAndTheReleaseIsOneToTheCandidate()
    {
        Assert.Null(await Checker(Release, """{"app":"1.2.0-rc.1"}""").CheckAsync(SemVer.Parse("1.2.0")!, default));
        Assert.NotNull(await Checker(Release, """{"app":"1.2.0"}""").CheckAsync(SemVer.Parse("1.2.0-rc.1")!, default));
    }
}
