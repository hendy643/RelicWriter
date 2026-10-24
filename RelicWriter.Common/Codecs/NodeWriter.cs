using RelicWriter.Common.Container;
using System.Buffers.Binary;
using System.Text;

namespace RelicWriter.Common.Codecs;

/// <summary>Little-endian writer producing a node payload; used by the generated codecs.</summary>
public sealed class NodeWriter
{
    readonly MemoryStream s = new();

    public byte[] ToArray() => s.ToArray();

    public void U8(byte x) => s.WriteByte(x);
    public void U16(ushort x) { Span<byte> b = stackalloc byte[sizeof(ushort)]; BinaryPrimitives.WriteUInt16LittleEndian(b, x); s.Write(b); }
    public void U32(uint x) { Span<byte> b = stackalloc byte[sizeof(uint)]; BinaryPrimitives.WriteUInt32LittleEndian(b, x); s.Write(b); }
    public void U64(ulong x) { Span<byte> b = stackalloc byte[sizeof(ulong)]; BinaryPrimitives.WriteUInt64LittleEndian(b, x); s.Write(b); }
    public void I32(int x) { Span<byte> b = stackalloc byte[sizeof(int)]; BinaryPrimitives.WriteInt32LittleEndian(b, x); s.Write(b); }
    public void F32(float x) { Span<byte> b = stackalloc byte[sizeof(float)]; BinaryPrimitives.WriteSingleLittleEndian(b, x); s.Write(b); }

    public void Bytes(byte[] x) => s.Write(x);

    public void Varint(int x) => SaveBinary.WriteVarint(s, x);

    public void String(string x)
    {
        SaveBinary.WriteVarint(s, -x.Length);
        s.Write(Encoding.Latin1.GetBytes(x));
    }

    public void CName(CNameValue x, uint saveVersion)
    {
        if (saveVersion >= SaveVersions.CNameIsHash) U64(x.Hash); else String(x.Text ?? "");
    }
}
