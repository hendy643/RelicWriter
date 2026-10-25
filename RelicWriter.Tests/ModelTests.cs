using RelicWriter.Common.Container;
using RelicWriter.Common.Model;
using RelicWriter.Tests.Fixtures;
using Xunit;

namespace RelicWriter.Tests;

public class ModelTests
{
    public static IEnumerable<object[]> GarageVersions() => SyntheticSave.Versions.Where(v => v.Save >= 205).Select(v => new object[] { v.Save });

    public static IEnumerable<object[]> RelicVersions() => SyntheticSave.Versions.Where(v => v.Save >= 254).Select(v => new object[] { v.Save });

    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void UneditedPlayerDevelopmentAndInventoryLeaveTheSaveUnchanged(uint version)
    {
        var file = SyntheticSave.Read(version);
        var before = file.Walk().Select(n => n.Data).ToList();
        PlayerDevelopment.Read(file)!.ApplyTo(file);
        Inventory.Read(file)!.ApplyTo();
        Assert.Equal(before, file.Walk().Select(n => n.Data));
    }

    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void CharacterSheetAndMoneyAreFound(uint version)
    {
        var file = SyntheticSave.Read(version);
        var dev = PlayerDevelopment.Read(file)!;
        Assert.Equal(30, dev.Level!.Level);
        Assert.Equal(20, dev.StreetCred!.Level);
        Assert.Equal(5, dev.Attributes.Count);
        Assert.Equal([10, 11, 12, 13, 14], dev.Attributes.Select(a => a.Value));
        Assert.Equal("Nomad", dev.LifePath);
        Assert.Equal(["Assault", "Athletics", "Hacking"], dev.Skills.Select(s => s.Type).Order());

        var inv = Inventory.Read(file)!;
        Assert.Equal(5000u, inv.Money!.Quantity);
        Assert.Equal(file.Walk().First(n => n.Name == "inventory").Children.Count, inv.SubInventories.Sum(s => s.Items.Count));
        Assert.Equal([1UL, 1_000_000UL], inv.SubInventories.Select(s => s.Id));
        Assert.Equal([2, 1], inv.SubInventories.Select(s => s.Items.Count));
    }

    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void EditsSurviveWritingAndReadingTheWholeFile(uint version)
    {
        var file = SyntheticSave.Read(version);
        var dev = PlayerDevelopment.Read(file)!;
        dev.Level!.Level = 42;
        dev.StreetCred!.Level = 33;
        dev.Attributes[0].Value = 17;
        dev.Points.First(p => p.Type == "Primary").Unspent = 123;
        dev.ApplyTo(file);
        var inv = Inventory.Read(file)!;
        inv.Money!.Quantity = 987654;
        inv.ApplyTo();

        var again = SaveFile.Read(file.Write());
        var dev2 = PlayerDevelopment.Read(again)!;
        Assert.Equal(42, dev2.Level!.Level);
        Assert.Equal(33, dev2.StreetCred!.Level);
        Assert.Equal(17, dev2.Attributes[0].Value);
        Assert.Equal(123, dev2.Points.First(p => p.Type == "Primary").Unspent);
        Assert.Equal(987654u, Inventory.Read(again)!.Money!.Quantity);
        // What was not edited is untouched.
        Assert.Equal(11, dev2.Attributes[1].Value);
        Assert.Equal(2, dev2.Points.First(p => p.Type == "Attribute").Unspent);
    }

    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void AddedItemGoesIntoTheBagAndSurvivesWritingAndReading(uint version)
    {
        var file = SyntheticSave.Read(version);
        foreach (var (n, i) in file.Walk().Select((n, i) => (n, i))) n.Id = (uint)i; // real saves number their nodes by position
        var inv = Inventory.Read(file)!;
        var shape = inv.SubInventories[0].Items[1].Shape; // the bag holds eddies and one other item
        var added = inv.Add(0xABCDEF01, 20, shape, 77)!;
        Assert.Equal(shape, added.Shape);
        Assert.Equal(file.Walk().Select((_, i) => (uint)i), file.Walk().Select(n => n.Id));

        var again = Inventory.Read(SaveFile.Read(file.Write()))!;
        Assert.Equal([1UL, 1_000_000UL], again.SubInventories.Select(s => s.Id));
        Assert.Equal([3, 1], again.SubInventories.Select(s => s.Items.Count));
        var yukimura = VehicleId.FromName("Items.Preset_Yukimura_Default").Hash;
        Assert.Equal(yukimura, again.SubInventories[0].Items[1].RecordHash);
        var last = again.SubInventories[0].Items[^1];
        Assert.Equal((0xABCDEF01u, (byte)20), (last.RecordHash, last.RecordLength));
        Assert.Equal(shape == ItemShape.Gear ? null : 77u, last.Quantity);
        Assert.Equal(5000u, again.Money!.Quantity);
        Assert.Equal(VehicleId.FromName("Items.FirstAidWhiffV0").Hash, again.SubInventories[1].Items[0].RecordHash);
    }

    [Theory, MemberData(nameof(GarageVersions))]
    public void VehicleUnlocksSurviveWritingAndReadingTheWholeFile(uint version)
    {
        var file = SyntheticSave.Read(version);
        var garage = VehicleGarage.Read(file)!;
        Assert.Equal(SyntheticSave.OwnedVehicles.Select(VehicleId.FromName), garage.Unlocked);

        var before = file.Walk().Select(n => n.Data).ToList();
        garage.ApplyTo();
        Assert.Equal(before, file.Walk().Select(n => n.Data));

        var all = VehicleCatalogue.Records.Select(VehicleId.FromName).ToList();
        foreach (var id in all) garage.Unlock(id);
        garage.ApplyTo();
        var again = SaveFile.Read(file.Write());
        var garage2 = VehicleGarage.Read(again)!;
        Assert.All(all, id => Assert.True(garage2.IsUnlocked(id)));
        Assert.Equal(all.Count, garage2.Unlocked.Distinct().Count());

        garage2.Lock(all[0]);
        garage2.ApplyTo();
        var garage3 = VehicleGarage.Read(SaveFile.Read(again.Write()))!;
        Assert.False(garage3.IsUnlocked(all[0]));
        Assert.True(garage3.IsUnlocked(all[1]));
        // The other system's entry in the same node is still there.
        var data = file.Walk().First(n => n.Name == "PersistencySystem2").Data;
        Assert.True(data.AsSpan().IndexOf(BitConverter.GetBytes(0xDEADu)) >= 0);
    }

    [Fact]
    public void SavesBefore205HaveNoReadableGarage() =>
        Assert.Null(VehicleGarage.Read(SyntheticSave.Read(193)));

    static PerkInfo Perk(string id) => Perks.All.Single(p => p.Id == id);

    [Theory, MemberData(nameof(RelicVersions))]
    public void PerksSurviveWritingAndReadingTheWholeFile(uint version)
    {
        var file = SyntheticSave.Read(version);
        var dev = PlayerDevelopment.Read(file)!;
        var relic = Perks.All.Where(p => p.IsRelic).ToList();
        Assert.True(dev.HasRelic);
        Assert.Equal(1, dev.PerkLevel(Perk("Espionage_Central_Milestone_1")));
        Assert.Equal(0, dev.PerkLevel(Perk("Espionage_Right_Perk_1_1")));
        Assert.Equal(1, dev.PerkLevel(Perk("Cool_Left_Perk_2_1")));

        foreach (var perk in relic) dev.SetPerkLevel(perk, perk.MaxLevel + 5); // clamped to the maximum
        dev.SetPerkLevel(Perk("Cool_Left_Perk_2_1"), 0);
        dev.ApplyTo(file);
        var again = PlayerDevelopment.Read(SaveFile.Read(file.Write()))!;
        Assert.All(relic, perk => Assert.Equal(perk.MaxLevel, again.PerkLevel(perk)));
        Assert.Equal(0, again.PerkLevel(Perk("Cool_Left_Perk_2_1")));

        again.SetPerkLevel(relic[0], 0);
        var file2 = SaveFile.Read(file.Write());
        again.ApplyTo(file2);
        var last = PlayerDevelopment.Read(SaveFile.Read(file2.Write()))!;
        Assert.Equal(0, last.PerkLevel(relic[0]));
        Assert.Equal(relic[1].MaxLevel, last.PerkLevel(relic[1]));
    }

    [Theory, MemberData(nameof(RelicVersions))]
    public void UnlockedPerksKeepALevelZeroEntryAndSoldPerksTakeTheirDependentsAway(uint version)
    {
        var file = SyntheticSave.Read(version);
        var dev = PlayerDevelopment.Read(file)!;
        dev.SetPerkLevel(Perk("Espionage_Central_Perk_1_2"), 1); // the synthetic character has the milestone and Perk_1_1 only
        dev.ApplyTo(file);
        var bought = PlayerDevelopment.Read(SaveFile.Read(file.Write()))!;
        // With the milestone bought the game has the perks that need it unlocked, at level 0.
        Assert.Contains(bought.PerkEntries, e => e.Perk == "Espionage_Central_Perk_1_3" && e.Level == 0);

        bought.SetPerkLevel(Perk("Espionage_Central_Milestone_1"), 0);
        var file2 = SaveFile.Read(file.Write());
        bought.ApplyTo(file2);
        var sold = PlayerDevelopment.Read(SaveFile.Read(file2.Write()))!;
        Assert.DoesNotContain(sold.PerkEntries, e => e.Perk == "Espionage_Central_Perk_1_3");
        Assert.Contains(sold.PerkEntries, e => e.Perk == "Espionage_Central_Milestone_1" && e.Level == 0);
    }

    [Fact]
    public void PerkCostsMatchTheGameDataTotals()
    {
        Assert.Equal(227, Perks.CostOfAll(relic: false));
        Assert.Equal(15, Perks.CostOfAll(relic: true));
    }

    [Theory]
    [InlineData(193u)]
    [InlineData(224u)]
    public void SavesWithoutThePhantomLibertyTreeHaveNoRelicPerks(uint version)
    {
        var dev = PlayerDevelopment.Read(SyntheticSave.Read(version))!;
        var perk = Perk("Espionage_Central_Milestone_1");
        Assert.False(dev.HasRelic);
        dev.SetPerkLevel(perk, 1); // nothing to change; must not throw
        Assert.Equal(0, dev.PerkLevel(perk));
    }

    [Fact]
    public void RecordNamesResolveKnownRecordsAndReadableNames()
    {
        Assert.Equal("Items.money", RecordNames.Record(Inventory.MoneyHash, 11));
        Assert.Null(RecordNames.Record(1, 1));
        Assert.Equal("Yukimura Default", RecordNames.Humanize("Items.Preset_Yukimura_Default"));
        Assert.Equal("Quadra Turbo R", RecordNames.Humanize("Vehicle.QuadraTurbo_R"));
    }

    [Fact]
    public void ItemsInASaveGetNames()
    {
        var items = Inventory.Read(SyntheticSave.Read(277))!.SubInventories.SelectMany(s => s.Items);
        Assert.All(items, i => Assert.NotNull(RecordNames.Display(i.RecordHash, i.RecordLength)));
    }

    [Fact]
    public void VehicleIdIsTheTweakDbIdOfTheRecordName() =>
        Assert.Equal(new VehicleId(Inventory.MoneyHash, 11), VehicleId.FromName("Items.money"));

    [Fact]
    public void EveryCatalogueVehicleSaysWhereItsPictureIsInTheGameFiles()
    {
        Assert.All(VehicleCatalogue.Records, r =>
        {
            var entry = VehicleCatalogue.Entry(r)!;
            Assert.NotNull(entry.Atlas);
            Assert.False(string.IsNullOrEmpty(entry.AtlasPart));
        });
    }

    [Fact]
    public void EveryPerkRequirementIsAPerkOfTheDatabaseAndRelicPerksNeedTheirMilestone()
    {
        var ids = Perks.All.Select(p => p.Id).ToHashSet();
        Assert.All(Perks.All, p => Assert.All(p.RequiredPerks, r => Assert.Contains(r, ids)));
        Assert.Equal(["Espionage_Central_Milestone_1"], Perks.All.First(p => p.Id == "Espionage_Central_Perk_1_1").RequiredPerks);
        Assert.Empty(Perks.All.First(p => p.Id == "Espionage_Central_Milestone_1").RequiredPerks);
        // The attribute value each perk's tier needs, from the game's tier records.
        foreach (var (id, points) in new[] { ("Body_Central_Perk_1_1", 4), ("Cool_Central_Perk_2_1", 9), ("Cool_Right_Perk_3_1", 15), ("Espionage_Central_Milestone_1", 0) })
            Assert.Equal(points, Perks.All.First(p => p.Id == id).AttributePoints);
    }
}
