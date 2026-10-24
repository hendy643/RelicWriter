using RelicWriter.Common;
using System.Buffers.Binary;
using System.Text;

namespace RelicWriter.GameAssets;

/// <summary>
/// The game's on-screen text (localization onscreens.json): a CR2W file holding about 62,000 entries, each a primaryKey (u64), an optional
/// secondaryKey (string) and a femaleVariant (string, the text shown for most entries) and sometimes a maleVariant.
/// </summary>
/// <remarks>
/// shortcut: this is not a CR2W parser. It resolves the property and type name indexes from the file's name table, then finds each entry by the
/// byte pattern of its primaryKey property and reads the string properties that follow. It matched all 62,229 entries of the 2.5 English file;
/// upgrade to a real CR2W reader if the file's structure changes.
/// </remarks>
public sealed class OnScreens
{
    // The header must reach the import table (0x40) before the name table can be trusted; names are 8-byte rows.
    const int MinHeaderSize = Cr2w.ImportsOffsetField, NameRowSize = 8;
    // primaryKey property: name index, type index, size, then the u64 key. The size is the 4 size bytes plus the 8 key bytes.
    const int PatternSize = 8, PatternTypeOffset = sizeof(ushort), PatternSizeOffset = 2 * sizeof(ushort), PrimaryKeyPropertySize = sizeof(uint) + sizeof(ulong);
    const int KeyOffset = 8, FirstFollowingProperty = KeyOffset + sizeof(ulong);
    // A length: first byte holds 6 value bits and a "more" bit; later bytes 7 value bits and a "more" bit.
    const int FirstValueBits = 6, NextValueBits = 7;
    const int FirstValueMask = 0x3F, FirstMoreBit = 0x40, NextValueMask = 0x7F, NextMoreBit = 0x80;
    readonly Dictionary<ulong, string> text = [];

    public string? Text(ulong primaryKey) => text.TryGetValue(primaryKey, out var t) ? t : null;

    public int Count => text.Count;

    /// <summary>Adds the entries of a file; later files replace entries with the same key. Returns false if the file is not the expected CR2W.</summary>
    public bool Add(byte[] cr2w)
    {
        // Header: "CR2W", then table descriptors (u32 offset, u32 count, u32 crc) from 0x28: 0 = string blob, 1 = names (u32 string offset, u32 hash).
        if (cr2w.Length < MinHeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(cr2w) != Cr2w.Magic) return false;
        var strings = (int)BinaryPrimitives.ReadUInt32LittleEndian(cr2w.AsSpan(Cr2w.StringsOffsetField));
        var names = (int)BinaryPrimitives.ReadUInt32LittleEndian(cr2w.AsSpan(Cr2w.NamesOffsetField));
        var nameCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(cr2w.AsSpan(Cr2w.NameCountField));
        var index = new Dictionary<string, ushort>();
        for (var i = 0; i < nameCount; i++)
        {
            var start = strings + (int)BinaryPrimitives.ReadUInt32LittleEndian(cr2w.AsSpan(names + i * NameRowSize));
            var end = Array.IndexOf(cr2w, (byte)0, start);
            index.TryAdd(Encoding.ASCII.GetString(cr2w, start, end - start), (ushort)i);
        }
        if (!index.TryGetValue("primaryKey", out var primary) || !index.TryGetValue("Uint64", out var u64) ||
            !index.TryGetValue("femaleVariant", out var female) || !index.TryGetValue("String", out var str)) return false;

        var pattern = new byte[PatternSize];
        BinaryPrimitives.WriteUInt16LittleEndian(pattern, primary);
        BinaryPrimitives.WriteUInt16LittleEndian(pattern.AsSpan(PatternTypeOffset), u64);
        BinaryPrimitives.WriteUInt32LittleEndian(pattern.AsSpan(PatternSizeOffset), PrimaryKeyPropertySize);

        var starts = new List<int>();
        for (var at = 0; at < cr2w.Length;)
        {
            var found = cr2w.AsSpan(at).IndexOf(pattern);
            if (found < 0) break;
            starts.Add(at + found);
            at += found + pattern.Length;
        }

        for (var e = 0; e < starts.Count; e++)
        {
            var key = BinaryPrimitives.ReadUInt64LittleEndian(cr2w.AsSpan(starts[e] + KeyOffset));
            var p = starts[e] + FirstFollowingProperty;
            var limit = e + 1 < starts.Count ? starts[e + 1] : cr2w.Length;
            // Following properties: name index, type index, size (counting its own 4 bytes), value; a name index of 0 ends the entry.
            while (p + Cr2w.PropertyValueOffset <= limit)
            {
                var name = BinaryPrimitives.ReadUInt16LittleEndian(cr2w.AsSpan(p));
                var type = BinaryPrimitives.ReadUInt16LittleEndian(cr2w.AsSpan(p + Cr2w.PropertyTypeOffset));
                var size = BinaryPrimitives.ReadUInt32LittleEndian(cr2w.AsSpan(p + Cr2w.PropertySizeOffset));
                if (name == 0 || size < Cr2w.SizeFieldSize || p + Cr2w.SizeFieldSize + size > limit) break;
                if (name == female && type == str) text[key] = ReadString(cr2w, p + Cr2w.PropertyValueOffset);
                p += Cr2w.SizeFieldSize + (int)size;
            }
        }
        return starts.Count > 0;
    }

    /// <summary>A string: a length (first byte: bit 7 flag, bit 6 "more", 6 value bits; later bytes: 7 value bits and bit 7 "more"), then UTF-8 text.</summary>
    static string ReadString(byte[] b, int p)
    {
        var length = ReadLength(b, ref p);
        return Encoding.UTF8.GetString(b, p, length);
    }

    /// <summary>Reads a string length and moves <paramref name="p"/> to the first byte of the text.</summary>
    internal static int ReadLength(byte[] b, ref int p)
    {
        int first = b[p++], length = first & FirstValueMask, shift = FirstValueBits;
        if ((first & FirstMoreBit) != 0)
            while (true)
            {
                int next = b[p++];
                length |= (next & NextValueMask) << shift;
                shift += NextValueBits;
                if ((next & NextMoreBit) == 0) break;
            }
        return length;
    }
}
