using RelicWriter.Common.Container;
using RelicWriter.Common.Formats;
using RelicWriter.Tests.Fixtures;
using Xunit;

namespace RelicWriter.Tests;

public class SystemBlobTests
{
    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void ParseThenWriteGivesTheSameBytes(uint version)
    {
        var node = SyntheticSave.Read(version).Walk().First(n => n.Name == "ScriptableSystemsContainer");
        Assert.True(node.Data.AsSpan().SequenceEqual(SystemBlob.Parse(node.Data).Write()));
    }

    [Theory, MemberData(nameof(SyntheticSave.AllVersions), MemberType = typeof(SyntheticSave))]
    public void StructArraysOfPlayerDevelopmentRewriteIdentically(uint version)
    {
        var node = SyntheticSave.Read(version).Walk().First(n => n.Name == "ScriptableSystemsContainer");
        var blob = SystemBlob.Parse(node.Data);
        foreach (var obj in blob.OfClass("PlayerDevelopmentData"))
            foreach (var array in new[] { "proficiencies", "attributes", "devPoints" })
                if (obj.Body.Find(array) is not null) obj.Body.SetStructs(array, obj.Body.GetStructs(array));
        Assert.True(node.Data.AsSpan().SequenceEqual(blob.Write()));
    }

    [Fact]
    public void StringsOf256CharactersOrMoreKeepTheirRealLength()
    {
        // The descriptor holds the length in 8 bits, so a long string stores its length modulo 256 (found in a PSData blob of a 1.05 save).
        var blob = new SystemBlob();
        var lengths = new[] { 3, 255, 256, 264, 511, 600 };
        foreach (var n in lengths) blob.Intern(new string((char)('a' + n % 26), n));
        var bytes = blob.Write();

        var again = SystemBlob.Parse(bytes);
        Assert.Equal(blob.Strings, again.Strings);
        Assert.Equal(bytes, again.Write());
    }

    [Fact]
    public void ABlobWhoseStringHasNoTerminatorIsRejected()
    {
        var blob = new SystemBlob();
        blob.Intern("abc");
        var bytes = blob.Write();
        bytes[^1] = (byte)'x'; // the last byte was the string's zero
        Assert.Throws<InvalidDataException>(() => SystemBlob.Parse(bytes));
    }
}
