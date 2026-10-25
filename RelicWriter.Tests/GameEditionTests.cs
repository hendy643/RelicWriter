using RelicWriter.Common.Model;
using RelicWriter.Tests.Fixtures;
using Xunit;

namespace RelicWriter.Tests;

/// <summary>Per-patch limits and features (docs/game-versions.md), and moving a save to a later game version in its header.</summary>
public class GameEditionTests
{
    [Fact]
    public void PatchesAreInReleaseOrder()
    {
        Assert.Equal(GameEdition.Patches.OrderBy(p => p.GameVersion), GameEdition.Patches);
        Assert.Equal(GameEdition.Patches.Count, GameEdition.Patches.Select(p => p.GameVersion).Distinct().Count());
    }

    [Theory]
    [InlineData(1630u, false, 50)]
    [InlineData(2000u, false, 50)]
    [InlineData(2000u, true, 60)]
    [InlineData(1630u, true, 50)]   // the expansion needs 2.0
    [InlineData(2500u, true, 60)]
    public void LevelCapNeedsTheExpansion(uint game, bool expansion, int cap) => Assert.Equal(cap, new GameEdition(game, expansion).MaxLevel);

    [Fact]
    public void SkillsFollowThePatchAndTheExpansion()
    {
        var old = new GameEdition(1630, false);
        var rework = new GameEdition(2100, false);
        var expansion = new GameEdition(2100, true);
        Assert.True(old.HasSkill("Gunslinger")); Assert.False(old.HasSkill("StrengthSkill"));
        Assert.False(rework.HasSkill("Gunslinger")); Assert.True(rework.HasSkill("StrengthSkill"));
        Assert.False(rework.HasSkill("Espionage")); Assert.True(expansion.HasSkill("Espionage"));
        Assert.Equal(20, GameEdition.MaxSkillLevel("Gunslinger"));
        Assert.Equal(60, GameEdition.MaxSkillLevel("CoolSkill"));
    }

    [Fact]
    public void OnlyPatchesWithTheSameSaveVersionAreReachable()
    {
        Assert.Equal(["1.61", "1.63"], new GameEdition(1600, false).ReachablePatches(224).Select(p => p.Name));
        Assert.Empty(new GameEdition(2500, false).ReachablePatches(277));
        Assert.Empty(new GameEdition(1500, false).ReachablePatches(220));
    }

    [Fact]
    public void ChangingTheGameVersionPatchesTheHeaderBytes()
    {
        var file = SyntheticSave.Read(224);
        file.Header = file.Header.WithGameVersion(1630);
        var again = Common.Container.SaveFile.Read(file.Write());
        Assert.Equal(1630u, again.Header.GameVersion);
        Assert.Equal(224u, again.Header.SaveVersion);
    }
}
