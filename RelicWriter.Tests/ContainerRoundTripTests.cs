using RelicWriter.Common.Codecs;
using RelicWriter.Common.Codecs.Generated;
using RelicWriter.Common.Container;
using RelicWriter.Common.Model;
using RelicWriter.Tests.Fixtures;
using Xunit;

namespace RelicWriter.Tests;

public class ContainerRoundTripTests
{
    static byte[] NodeTable(byte[] file) => file[(int)BitConverter.ToUInt32(file, file.Length - 8)..^8];

    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void WriteThenReadGivesTheSameTree(uint version)
    {
        var original = SyntheticSave.Bytes(version);
        var a = SaveFile.Read(original);
        var rewritten = a.Write();
        var b = SaveFile.Read(rewritten);

        // The node table (names, links, logical offsets, sizes) must come out byte for byte the same.
        Assert.Equal(NodeTable(original), NodeTable(rewritten));

        Assert.Equal(version, b.Header.SaveVersion);
        var na = a.Walk().ToList();
        var nb = b.Walk().ToList();
        Assert.Equal(na.Count, nb.Count);
        for (var i = 0; i < na.Count; i++)
        {
            Assert.Equal(na[i].Name, nb[i].Name);
            Assert.Equal(na[i].Id, nb[i].Id);
            Assert.Equal(na[i].Data, nb[i].Data);
            Assert.Equal(na[i].Trailing, nb[i].Trailing);
            Assert.Equal(na[i].After, nb[i].After);
            Assert.Equal(na[i].Children.Count, nb[i].Children.Count);
        }
    }

    [Fact]
    public void BytesBetweenSiblingsAndAfterTheLastChildAreKept()
    {
        var file = SyntheticSave.Read(277);
        var inventory = file.Walk().First(n => n.Name == "inventory");
        Assert.NotEmpty(inventory.Children[0].After);
        Assert.NotEmpty(file.Walk().First(n => n.Name == "TimeSystem").Trailing);
    }

    [Fact]
    public void ARandomFileIsRejected() =>
        Assert.Throws<InvalidDataException>(() => SaveFile.Read(new byte[200]));

    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void DomainLoadThenApplyLeavesNodesUnchanged(uint version)
    {
        var file = SyntheticSave.Read(version);
        var before = file.Walk().Select(n => n.Data).ToList();
        SaveGame.Load(file).ApplyTo(file);
        Assert.Equal(before, file.Walk().Select(n => n.Data));
    }

    [Theory]
    [InlineData("PersistencySystem2", 224, true)]
    [InlineData("PersistencySystem2", 193, false)]
    [InlineData("PathInstance_Quest_v1", 193, true)]
    [InlineData("TypeDatabase_v2", 254, true)]
    [InlineData("TypeDatabase_v2", 224, false)]
    [InlineData("PhotoMode_BugFixing", 269, false)]
    [InlineData("PhotoMode_BugFixing", 277, true)]
    [InlineData("NotARealNode", 277, false)]
    public void NodeAvailabilityFollowsTheObservedVersions(string node, uint version, bool expected) =>
        Assert.Equal(expected, NodeAvailability.IsExpected(node, version));

    // ---- generated codecs, on payloads written by hand to the layouts in docs/save-spec.xml ----

    static byte[] Bytes(Action<BinaryWriter, MemoryStream> write)
    {
        var ms = new MemoryStream();
        write(new BinaryWriter(ms), ms);
        return ms.ToArray();
    }

    // Strings in node payloads: a varint of minus the length (a byte with bit 7 set and the length below 64), then the characters.
    static void Str(BinaryWriter w, string s) { w.Write((byte)(0x80 | s.Length)); w.Write(System.Text.Encoding.Latin1.GetBytes(s)); }

    static void AssertRoundTrips(string node, uint save, byte[] data) =>
        Assert.Equal(data, GeneratedCodecs.RoundTrips[node](data, new SaveContext(save, 0, 0)));

    [Theory]
    [InlineData(193u, 0)]
    [InlineData(224u, 1)]  // a save from game 1.6 has one value in the list
    [InlineData(277u, 0)]
    public void DynamicEntityIdSystemReadsItsCountedList(uint save, int listed) => AssertRoundTrips("DynamicEntityIDSystem", save, Bytes((w, _) =>
    {
        w.Write(10_000_001u); w.Write(9_000_001u);
        w.Write((uint)listed);
        for (var i = 0; i < listed; i++) w.Write(9_999_999u);
        w.Write(1u); Str(w, "p_q001_04_ripperdoc"); w.Write(77u);
        w.Write(0u);
    }));

    [Theory]
    [InlineData(193u, 0)]
    [InlineData(193u, 1)]  // one item
    [InlineData(277u, 1)]
    [InlineData(277u, 6)]  // six items: the other five headers are not in this node, so only the first is read
    public void ItemDropStorageReadsTheFirstItemHeader(uint save, int items) => AssertRoundTrips("ItemDropStorage", save, Bytes((w, _) =>
    {
        Str(w, "None"); w.Write((byte)0); w.Write(0u);
        w.Write(new byte[12]); w.Write(1f); w.Write(0f); w.Write(0f); w.Write(0f);
        w.Write((uint)items);
        if (items == 0) return;
        w.Write(0x1E_0000_0001UL); w.Write(5u);
        if (save > 96) w.Write((byte)0);
        if (save > 189) w.Write((ushort)0);
        if (save > 220) w.Write((byte)0);
    }));

    [Theory]
    [InlineData(193u)]
    [InlineData(224u)]  // the second list is not in saves up to 224
    [InlineData(261u)]
    [InlineData(277u)]
    public void ChoicesHasItsSecondListOnlyAfterVersion224(uint save) => AssertRoundTrips("choices", save, Bytes((w, _) =>
    {
        w.Write(1u); w.Write(42UL); w.Write(1u); w.Write(1u); w.Write(2u); w.Write(3u);
        if (save <= 224) return;
        w.Write(1u); w.Write(43UL); w.Write(1u); w.Write(9u); w.Write(1u); w.Write(new byte[16]);
    }));
}
