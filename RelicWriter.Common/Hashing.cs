using System.Text;

namespace RelicWriter.Common;

/// <summary>The two hashes the game's data uses: CRC-32 for TweakDBIDs and FNV-1a 64-bit for names, classes and file paths.</summary>
internal static class Hashing
{
    const ulong FnvOffsetBasis = 0xcbf29ce484222325UL, FnvPrime = 0x100000001b3UL;
    const uint Crc32Polynomial = 0xEDB88320u, Crc32Initial = 0xFFFFFFFFu;
    const int BitsPerByte = 8;

    public static ulong Fnv1a64(ReadOnlySpan<byte> data)
    {
        var h = FnvOffsetBasis;
        foreach (var b in data) h = (h ^ b) * FnvPrime;
        return h;
    }

    /// <summary>FNV-1a 64-bit of an ASCII name (property and class names in saves).</summary>
    public static ulong Fnv1a64(string name) => Fnv1a64(Encoding.ASCII.GetBytes(name));

    public static uint Crc32(ReadOnlySpan<byte> data) => ~Crc32Update(Crc32Initial, data);

    /// <summary>Continues a CRC-32 (in its running, inverted form) over more bytes; CRC-32 is incremental, so a suffix can be added without the original text.</summary>
    public static uint Crc32Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < BitsPerByte; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ Crc32Polynomial : crc >> 1;
        }
        return crc;
    }
}
