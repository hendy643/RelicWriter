using System.Text;

namespace RelicWriter.Common.Container;

/// <summary>The save format's varint (see docs/save-format.md): bit 7 sign, bit 6 continue, 6 value bits; later bytes 7 bits + continue flag in bit 7.</summary>
public static class SaveBinary
{
    const int FirstValueBits = 6, NextValueBits = 7;
    const byte SignBit = 0x80, FirstContinueBit = 0x40, FirstValueMask = 0x3f, NextContinueBit = 0x80, NextValueMask = 0x7f;

    public static int ReadVarint(ReadOnlySpan<byte> s, ref int p)
    {
        var c = s[p++];
        var x = c & FirstValueMask;
        var neg = (c & SignBit) != 0;
        if ((c & FirstContinueBit) != 0)
        {
            for (var shift = FirstValueBits; ; shift += NextValueBits)
            {
                c = s[p++];
                x |= (c & NextValueMask) << shift;
                if ((c & NextContinueBit) == 0) break;
            }
        }
        return neg ? -x : x;
    }

    public static void WriteVarint(Stream o, int value)
    {
        var x = (uint)Math.Abs(value);
        var first = (byte)((x & FirstValueMask) | (value < 0 ? SignBit : 0u));
        x >>= FirstValueBits;
        if (x == 0) { o.WriteByte(first); return; }
        o.WriteByte((byte)(first | FirstContinueBit));
        while (true)
        {
            var b = (byte)(x & NextValueMask);
            x >>= NextValueBits;
            if (x == 0) { o.WriteByte(b); return; }
            o.WriteByte((byte)(b | NextContinueBit));
        }
    }

    public static string ReadName(ReadOnlySpan<byte> s, ref int p)
    {
        var len = Math.Abs(ReadVarint(s, ref p)); // ASCII strings store a negative length
        var name = Encoding.Latin1.GetString(s.Slice(p, len));
        p += len;
        return name;
    }

    public static void WriteName(Stream o, string name)
    {
        WriteVarint(o, -name.Length);
        o.Write(Encoding.Latin1.GetBytes(name));
    }
}
