using RelicWriter.Common.GameData;
using Xunit;

namespace RelicWriter.Tests;

/// <summary>The launcher config parsers, on sample text in the formats the launchers write.</summary>
public class GameLocatorTests
{
    [Fact]
    public void SteamLibrariesAreReadFromTheVdf()
    {
        const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\Games\\\\Steam\"\n\t}\n}";
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\Games\Steam"], GameLocator.SteamLibraries(vdf));
    }

    [Fact]
    public void SteamInstallDirComesFromTheAppManifest()
    {
        Assert.Equal("Cyberpunk 2077 Renamed", GameLocator.SteamInstallDir("\"AppState\"\n{\n\t\"appid\"\t\t\"1091500\"\n\t\"installdir\"\t\t\"Cyberpunk 2077 Renamed\"\n}"));
        Assert.Null(GameLocator.SteamInstallDir(""));
    }

    [Fact]
    public void LutrisGivesTheFolderAboveTheExeAndThePrefixGuesses()
    {
        const string yaml = "game_slug: cyberpunk-2077\ngame:\n  exe: /home/v/Games/cp/bin/x64/Cyberpunk2077.exe\n  prefix: /home/v/Games/cp-prefix\n";
        var paths = GameLocator.LutrisPaths(yaml).ToList();
        Assert.Equal("/home/v/Games/cp", paths[0]);
        Assert.Contains(paths, p => p.Replace('\\', '/') == "/home/v/Games/cp-prefix/drive_c/GOG Games/Cyberpunk 2077");
    }

    [Fact]
    public void LutrisGivesThePrefixOfCyberpunkOnly()
    {
        Assert.Equal("/home/v/Games/cp-prefix", GameLocator.LutrisPrefix("game_slug: cyberpunk-2077\ngame:\n  prefix: /home/v/Games/cp-prefix\n"));
        Assert.Null(GameLocator.LutrisPrefix("game_slug: witcher-3\ngame:\n  prefix: /x\n"));
    }

    [Fact]
    public void HeroicGamesConfigGivesThePrefixAndItsPfx()
    {
        const string json = "{\"1423049311\":{\"winePrefix\":\"/home/v/Games/Heroic/Prefixes/cp\",\"other\":1},\"version\":\"v0\"}";
        Assert.Equal(["/home/v/Games/Heroic/Prefixes/cp", "/home/v/Games/Heroic/Prefixes/cp/pfx"], GameLocator.HeroicPrefixes(json).Select(p => p.Replace('\\', '/')));
        Assert.Empty(GameLocator.HeroicPrefixes(""));
    }

    [Fact]
    public void LutrisIgnoresOtherGames() => Assert.Empty(GameLocator.LutrisPaths("game_slug: witcher-3\ngame:\n  exe: /x/witcher3.exe\n"));

    [Fact]
    public void HeroicGogListGivesTheCyberpunkInstall()
    {
        const string json = "{\"installed\":[{\"appName\":\"1423049311\",\"install_path\":\"/home/v/Games/Heroic/Cyberpunk 2077\"},{\"appName\":\"999\",\"install_path\":\"/x/Other\"}]}";
        Assert.Equal(["/home/v/Games/Heroic/Cyberpunk 2077"], GameLocator.HeroicPaths(json));
    }

    [Fact]
    public void HeroicEpicListIsKeyedByAppName()
    {
        const string json = "{\"Ginger\":{\"install_path\":\"/e/cp\",\"title\":\"Cyberpunk 2077\"},\"Other\":{\"install_path\":\"/e/other\"}}";
        Assert.Equal(["/e/cp"], GameLocator.HeroicPaths(json));
    }

    [Fact]
    public void BrokenJsonGivesNothing()
    {
        Assert.Empty(GameLocator.HeroicPaths("not json"));
        Assert.Empty(GameLocator.EpicPaths(""));
    }

    [Fact]
    public void EpicManifestGivesTheInstallLocation()
    {
        Assert.Equal([@"E:\Epic\Cyberpunk 2077"], GameLocator.EpicPaths("{\"DisplayName\":\"Cyberpunk 2077\",\"InstallLocation\":\"E:\\\\Epic\\\\Cyberpunk 2077\"}"));
        Assert.Empty(GameLocator.EpicPaths("{\"DisplayName\":\"Other\",\"InstallLocation\":\"E:\\\\x\"}"));
    }
}
