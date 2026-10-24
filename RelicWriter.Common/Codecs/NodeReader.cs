using RelicWriter.Common.Container;
using System.Buffers.Binary;
using System.Text;

namespace RelicWriter.Common.Codecs;

/// <summary>Sequential little-endian reader over a node payload; used by the generated codecs.</summary>
public ref struct NodeReader(ReadOnlySpan<byte> data)
{
    ReadOnlySpan<byte> data = data;
    int pos;

    public readonly int Remaining => data.Length - pos;

    public byte U8() => data[pos++];
    public ushort U16() { var x = BinaryPrimitives.ReadUInt16LittleEndian(data[pos..]); pos += sizeof(ushort); return x; }
    public uint U32() { var x = BinaryPrimitives.ReadUInt32LittleEndian(data[pos..]); pos += sizeof(uint); return x; }
    public ulong U64() { var x = BinaryPrimitives.ReadUInt64LittleEndian(data[pos..]); pos += sizeof(ulong); return x; }
    public int I32() { var x = BinaryPrimitives.ReadInt32LittleEndian(data[pos..]); pos += sizeof(int); return x; }
    public float F32() { var x = BinaryPrimitives.ReadSingleLittleEndian(data[pos..]); pos += sizeof(float); return x; }

    public byte[] Bytes(int n) { var x = data.Slice(pos, n).ToArray(); pos += n; return x; }

    public int Varint() => SaveBinary.ReadVarint(data, ref pos);

    public string String()
    {
        var n = Varint();
        if (n > 0) throw new InvalidDataException("Positive string length is not supported.");
        var s = Encoding.Latin1.GetString(data.Slice(pos, -n));
        pos -= n;
        return s;
    }

    public CNameValue CName(uint saveVersion) => saveVersion >= SaveVersions.CNameIsHash ? new CNameValue(U64(), null) : new CNameValue(0, String());
}
