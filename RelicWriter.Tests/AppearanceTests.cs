using RelicWriter.Common.Container;
using RelicWriter.Common.Model;
using RelicWriter.Tests.Fixtures;
using Xunit;

namespace RelicWriter.Tests;

public class AppearanceTests
{
    static SaveFile Fixture(uint version, uint archive) => SaveFile.Read(SyntheticSave.Create(version, archive).Write());

    public static IEnumerable<object[]> Cases() =>
        from v in SyntheticSave.Versions
        from archive in new[] { SyntheticSave.DefaultArchive, SyntheticSave.HashedPartArchive }
        select new object[] { v.Save, archive };

    [Theory, MemberData(nameof(Cases))]
    public void PresetsAndPartsAreRead(uint version, uint archive)
    {
        var a = Appearance.Read(Fixture(version, archive))!;
        Assert.Equal(["eyes", "jaw"], a.PresetSlots);
        Assert.Equal(41, a.Preset("eyes"));
        Assert.Equal(64, a.Preset("jaw"));
        Assert.Null(a.Preset("nose"));
        Assert.Equal(version >= 168 ? ["skin_type_05", "hair_color50"] : ["", ""], a.CreatorParts.Select(p => p.Tag));
    }

    [Theory, MemberData(nameof(Cases))]
    public void UneditedAppearanceLeavesTheNodeUnchanged(uint version, uint archive)
    {
        var file = Fixture(version, archive);
        var before = file.Write();
        Appearance.Read(file)!.ApplyTo();
        Assert.Equal(before, file.Write());
    }

    [Theory, MemberData(nameof(Cases))]
    public void ChangingAPresetChangesEveryGroupThatHasIt(uint version, uint archive)
    {
        var file = Fixture(version, archive);
        var a = Appearance.Read(file)!;
        a.SetPreset("eyes", 7);
        a.ApplyTo();

        var again = Appearance.Read(SaveFile.Read(file.Write()))!;
        Assert.Equal(7, again.Preset("eyes"));
        Assert.Equal(64, again.Preset("jaw"));
        Assert.All(again.CharacterGroups, g => Assert.Equal("h007", g.Choices.First(c => c.Slot == "eyes").Value));
    }

    [Fact]
    public void ANodeThatDoesNotRoundTripIsNotOffered()
    {
        var file = Fixture(277, SyntheticSave.HashedPartArchive);
        var node = file.Walk().First(n => n.Name == Appearance.NodeName);
        node.Data = [.. node.Data, 0]; // one stray byte
        Assert.Null(Appearance.Read(file));
    }

    [Fact]
    public void ASaveWithoutTheNodeHasNoAppearance()
    {
        var file = Fixture(277, SyntheticSave.DefaultArchive);
        file.Roots.RemoveAll(n => n.Name == Appearance.NodeName);
        Assert.Null(Appearance.Read(file));
    }
}
