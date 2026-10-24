using System.Text;
using RelicWriter.Common.GameData;
using RelicWriter.GameAssets;
using RelicWriter.Common.Model;
using Xunit;

namespace RelicWriter.Tests;

/// <summary>The game-data readers, on files built here in the layout docs/game-data.md describes. Nothing is read from a game install.</summary>
public class GameDataTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("relicwriter-tests").FullName;

    public void Dispose() => Directory.Delete(dir, true);

    static ulong Fnv(string s)
    {
        var h = 0xcbf29ce484222325UL;
        foreach (var c in s) h = (h ^ (byte)c) * 0x100000001b3UL;
        return h;
    }

    static ulong Flat(string name) { var id = VehicleId.FromName(name); return TweakDb.Id(id.Hash, id.Length); }

    /// <summary>A type block: u32 value count, the values, u32 key count, then the keys (12 bytes: hash, length, 3 pad, value index) sorted by hash.</summary>
    static byte[] Block(IList<byte[]> values, IEnumerable<(ulong Flat, int Value)> keys)
    {
        var o = new MemoryStream();
        var w = new BinaryWriter(o);
        w.Write((uint)values.Count);
        foreach (var v in values) w.Write(v);
        var sorted = keys.OrderBy(k => (uint)k.Flat).ToList();
        w.Write((uint)sorted.Count);
        foreach (var (flat, value) in sorted) { w.Write((uint)flat); w.Write((byte)(flat >> 32)); w.Write(new byte[3]); w.Write((uint)value); }
        return o.ToArray();
    }

    string WriteTweakDb(string name, params (string Type, byte[] Block, int Keys, int Values)[] types)
    {
        var o = new MemoryStream();
        var w = new BinaryWriter(o);
        w.Write(0x0BB1DB47u); w.Write(8u);
        w.Write(new byte[0x14 - 8]);
        var recordsAtPosition = (int)o.Position;
        w.Write(0u);
        w.Write(new byte[0x20 - 0x18]);
        w.Write((uint)types.Length);
        var entriesAt = (int)o.Position;
        w.Write(new byte[20 * types.Length]);
        var offsets = new List<int>();
        foreach (var t in types) { offsets.Add((int)o.Position); w.Write(t.Block); }
        var recordsAt = (int)o.Position;
        w.Write(new byte[8]);

        var bytes = o.ToArray();
        BitConverter.GetBytes((uint)recordsAt).CopyTo(bytes, recordsAtPosition);
        for (var i = 0; i < types.Length; i++)
        {
            var e = entriesAt + 20 * i;
            BitConverter.GetBytes(Fnv(types[i].Type)).CopyTo(bytes, e);
            BitConverter.GetBytes((uint)types[i].Values).CopyTo(bytes, e + 8);
            BitConverter.GetBytes((uint)types[i].Keys).CopyTo(bytes, e + 12);
            BitConverter.GetBytes((uint)offsets[i]).CopyTo(bytes, e + 16);
        }
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    static byte[] U32Pair(uint a, uint b) => [.. BitConverter.GetBytes(a), .. BitConverter.GetBytes(b)];

    /// <summary>A TweakDB with a vehicle whose name, icon, atlas and part are set, plus a second vehicle with only a name.</summary>
    string TestTweakDb(string name = "tweakdb.bin", uint nameKey = 7)
    {
        var icon = VehicleId.FromName("UIIcon.test_car");
        var locKeys = Block([U32Pair(nameKey, 0), U32Pair(9, 0), U32Pair(0, 0)],
            [(Flat("Vehicle.test_car.displayName"), 0), (Flat("Vehicle.second.displayName"), 1), (Flat("Vehicle.empty.displayName"), 2)]);
        var links = Block([U32Pair(icon.Hash, icon.Length)], [(Flat("Vehicle.test_car.icon"), 0)]);
        var resources = Block([BitConverter.GetBytes(0xDF7FA21BB9602583UL)], [(Flat("UIIcon.test_car.atlasResourcePath"), 0)]);
        var names = new MemoryStream();
        var part = Encoding.ASCII.GetBytes("test_part");
        names.Write(BitConverter.GetBytes(1u));
        names.WriteByte((byte)part.Length); names.Write(part);
        names.Write(BitConverter.GetBytes(1u));
        names.Write(BitConverter.GetBytes((uint)Flat("UIIcon.test_car.atlasPartName"))); names.WriteByte((byte)(Flat("UIIcon.test_car.atlasPartName") >> 32)); names.Write(new byte[3]); names.Write(BitConverter.GetBytes(0u));
        var text = Encoding.ASCII.GetBytes("LocKey#12");
        var strings = new MemoryStream();
        strings.Write(BitConverter.GetBytes(1u));
        strings.WriteByte((byte)text.Length); strings.Write(text);
        strings.Write(BitConverter.GetBytes(1u));
        strings.Write(BitConverter.GetBytes((uint)Flat("NewPerks.test.loc_name_key"))); strings.WriteByte((byte)(Flat("NewPerks.test.loc_name_key") >> 32)); strings.Write(new byte[3]); strings.Write(BitConverter.GetBytes(0u));
        // Two arrays: the first holds two ids; the second holds 70, whose count takes two bytes (6 bits, a continuation flag, then the rest).
        var two = new byte[] { 2 }.Concat(U32Pair(1, 5)).Concat(U32Pair(2, 6)).ToArray();
        var seventy = new byte[] { 70 & 0x3F | 0x40, 70 >> 6 }.Concat(Enumerable.Range(0, 70).SelectMany(i => U32Pair((uint)i + 100, 7))).ToArray();
        var arrays = Block([two, seventy], [(Flat("NewPerks.test.requiresPerks"), 0), (Flat("NewPerks.big.requiresPerks"), 1)]);
        var ints = Block([BitConverter.GetBytes(-9)], [(Flat("NewPerks.test.value"), 0)]);
        return WriteTweakDb(name,
            ("gamedataLocKeyWrapper", locKeys, 3, 3), ("TweakDBID", links, 1, 1), ("raRef:CResource", resources, 1, 1), ("CName", names.ToArray(), 1, 1), ("String", strings.ToArray(), 1, 1),
            ("array:TweakDBID", arrays, 2, 2), ("Int32", ints, 1, 1));
    }

    [Fact]
    public void TweakDbIdOfASuffixedNameIsFoundWithoutTheName()
    {
        var money = TweakDb.Id(Inventory.MoneyHash, 11);
        Assert.Equal(Flat("Items.money.displayName"), TweakDb.Append(money, ".displayName"));
    }

    [Fact]
    public void TweakDbGivesTextKeysLinksResourcesAndNames()
    {
        var db = TweakDb.Open([TestTweakDb()])!;
        var car = Flat("Vehicle.test_car");
        Assert.Equal(7u, db.LocKey(TweakDb.Append(car, ".displayName")));
        Assert.Equal(9u, db.LocKey(Flat("Vehicle.second.displayName")));
        Assert.Null(db.LocKey(Flat("Vehicle.empty.displayName")));  // an empty key is no name
        Assert.Null(db.LocKey(Flat("Vehicle.unknown.displayName")));

        var icon = db.Link(TweakDb.Append(car, ".icon"))!.Value;
        Assert.Equal(Flat("UIIcon.test_car"), icon);
        Assert.Equal(0xDF7FA21BB9602583UL, db.Resource(TweakDb.Append(icon, ".atlasResourcePath")));
        Assert.Equal("test_part", db.Name(TweakDb.Append(icon, ".atlasPartName")));
        Assert.Equal("LocKey#12", db.String(Flat("NewPerks.test.loc_name_key")));
        Assert.Null(db.String(Flat("NewPerks.other.loc_name_key")));
    }

    [Fact]
    public void TweakDbGivesIntegersAndIdArrays()
    {
        var db = TweakDb.Open([TestTweakDb()])!;
        Assert.Equal(-9, db.Int(Flat("NewPerks.test.value")));
        Assert.Null(db.Int(Flat("NewPerks.test.other")));
        Assert.Equal([TweakDb.Id(1, 5), TweakDb.Id(2, 6)], db.Links(Flat("NewPerks.test.requiresPerks"))!);
        var big = db.Links(Flat("NewPerks.big.requiresPerks"))!;
        Assert.Equal(70, big.Length);
        Assert.Equal(TweakDb.Id(169, 7), big[^1]);
        Assert.Null(db.Links(Flat("NewPerks.test.other")));
    }

    [Fact]
    public void ALaterTweakDbFileReplacesAFlatOfAnEarlierOne()
    {
        var db = TweakDb.Open([TestTweakDb("base.bin", 7), TestTweakDb("expansion.bin", 8)])!;
        Assert.Equal(8u, db.LocKey(Flat("Vehicle.test_car.displayName")));
    }

    [Fact]
    public void AFileThatIsNotATweakDbIsRefused()
    {
        var path = Path.Combine(dir, "other.bin");
        File.WriteAllBytes(path, new byte[100]);
        Assert.Null(TweakDb.Open([path]));
    }

    [Fact]
    public void Bc3BlockDecodesToItsColourAndAlpha()
    {
        // One 4x4 block: alpha 255 for every pixel (alpha0 above alpha1, all indices 0), colour0 pure red, colour indices 0.
        byte[] block = [255, 0, 0, 0, 0, 0, 0, 0, 0x00, 0xF8, 0x00, 0x00, 0, 0, 0, 0];
        var rgba = Bc3.DecodeRegion(block, 4, 0, 0, 4, 4);
        for (var i = 0; i < 16; i++) Assert.Equal(new byte[] { 255, 0, 0, 255 }, rgba.AsSpan(i * 4, 4).ToArray());
    }

    [Fact]
    public void Bc3RegionPicksTheRightBlock()
    {
        // Texture 8 wide, 4 high: the left block is red, the right one blue; a 2x2 region at x=4 must be blue.
        byte[] red = [255, 0, 0, 0, 0, 0, 0, 0, 0x00, 0xF8, 0x00, 0x00, 0, 0, 0, 0];
        byte[] blue = [255, 0, 0, 0, 0, 0, 0, 0, 0x1F, 0x00, 0x00, 0x00, 0, 0, 0, 0];
        var rgba = Bc3.DecodeRegion([.. red, .. blue], 8, 4, 0, 2, 2);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, rgba.AsSpan(0, 4).ToArray());
    }

    [Fact]
    public void ArchivePathHashesAreFnv1aOfTheLowerCasePath()
    {
        const string path = @"base\gameplay\gui\common\iconsehicles_icons.xbm";
        Assert.Equal(Fnv(path), GameArchive.PathHash(path));
        Assert.Equal(Fnv(path), GameArchive.PathHash(path.ToUpperInvariant()));
    }
}
