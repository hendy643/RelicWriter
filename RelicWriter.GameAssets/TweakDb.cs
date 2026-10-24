using RelicWriter.Common;
using System.Buffers.Binary;
using System.Text;

namespace RelicWriter.GameAssets;

/// <summary>
/// The parts of the game's TweakDB (r6/cache/tweakdb.bin, blob version 8) that RelicWriter needs: flats holding a localisation key
/// (gamedataLocKeyWrapper, for example an item's displayName), a link to another record (TweakDBID, for example a vehicle's icon record),
/// a resource path hash (raRef:CResource) or a name (CName). A flat is identified by the TweakDBID of "Record.name"; see <see cref="Append"/>.
/// </summary>
/// <remarks>
/// Layout as read from the game's files: u32 magic 0x0BB1DB47, then version fields, then at 0x14 the offset of the records section and at 0x20 a
/// u32 type count and 20-byte entries (u64 FNV-1a hash of the type name, u32 value count, u32 key count, u32 data offset). Each type's data is
/// u32 value count, the values, u32 key count, then the keys, sorted by name hash. A key is 12 bytes: u32 name hash, u8 name length,
/// 3 padding bytes, u32 index into the values. Values are shared, so there are more keys than values. Values are 8 bytes for the three
/// types above except CName, whose values are length-prefixed strings (see <see cref="OnScreens"/> for the length encoding).
/// Checked on the 2.5 files only.
/// </remarks>
public sealed class TweakDb
{
    const uint Magic = 0x0BB1DB47, SupportedVersion = 8;
    // Header: magic, version, the records offset at 0x14 and the type count at 0x20; the type entries follow at 0x24.
    const int VersionOffset = 4, RecordsOffsetField = 0x14, TypeCountOffset = 0x20, HeaderSize = 0x24;
    // Type entry: u64 type hash, u32 value count, u32 key count (at 12), u32 data offset (at 16).
    const int TypeEntrySize = 20, TypeKeyCountOffset = 12, TypeDataOffsetOffset = 16;
    const int CountSize = sizeof(uint);
    // A value is 8 bytes. A LocKey value is the key, then a secondary hash; a link value is the record hash, then its length.
    const int ValueSize = sizeof(ulong), SecondaryHashOffset = sizeof(uint), LinkLengthOffset = sizeof(uint);

    /// <summary>One file's tables; files are searched newest first so Phantom Liberty can replace a base game flat.</summary>
    sealed record Tables(FlatTable LocKeys, FlatTable Links, FlatTable Resources, FlatTable CNames, string[] CNamePool, FlatTable Strings, string[] StringPool, FlatTable Ints, FlatTable Arrays, int[] ArrayAt);

    readonly List<Tables> files = [];

    public static ulong Id(uint hash, int length) => hash | (ulong)length << 32;

    /// <summary>The id of the flat "<c>record</c>.name": the record's CRC-32 carried on over the suffix. CRC-32 is incremental, so the record's name isn't needed.</summary>
    public static ulong Append(ulong record, string suffix)
    {
        var crc = Hashing.Crc32Update(~(uint)record, Encoding.ASCII.GetBytes(suffix));
        return Id(~crc, (int)(record >> 32) + suffix.Length);
    }

    /// <summary>Reads the files in order; a flat in a later file replaces the same flat in an earlier one. Null if a file is not a version 8 TweakDB.</summary>
    public static TweakDb? Open(IEnumerable<string> paths)
    {
        var db = new TweakDb();
        foreach (var path in paths)
        {
            if (Load(path) is not { } tables) return null;
            db.files.Insert(0, tables);
        }
        return db;
    }

    /// <summary>The localisation key a flat points to, or null (also for an empty key, and for the few keys that carry a secondary hash, which are not resolved).</summary>
    public uint? LocKey(ulong flat)
    {
        foreach (var f in files)
            if (f.LocKeys.Find(flat) is var i and >= 0)
                return f.LocKeys.U32(i, SecondaryHashOffset) == 0 && f.LocKeys.U32(i, 0) != 0 ? f.LocKeys.U32(i, 0) : null;
        return null;
    }

    /// <summary>The record id a TweakDBID flat points to.</summary>
    public ulong? Link(ulong flat)
    {
        foreach (var f in files)
            if (f.Links.Find(flat) is var i and >= 0) return Id(f.Links.U32(i, 0), f.Links.Byte(i, LinkLengthOffset));
        return null;
    }

    /// <summary>The path hash a resource flat points to (the same hash <see cref="GameArchive.PathHash"/> gives).</summary>
    public ulong? Resource(ulong flat)
    {
        foreach (var f in files)
            if (f.Resources.Find(flat) is var i and >= 0) return f.Resources.U64(i);
        return null;
    }

    /// <summary>The text of a CName flat.</summary>
    public string? Name(ulong flat)
    {
        foreach (var f in files)
            if (f.CNames.Find(flat) is var i and >= 0) return f.CNamePool[f.CNames.ValueIndex(i)];
        return null;
    }

    /// <summary>The text of a String flat (for example a perk's loc_name_key, which holds a "LocKey#number" or a secondary key).</summary>
    public string? String(ulong flat)
    {
        foreach (var f in files)
            if (f.Strings.Find(flat) is var i and >= 0) return f.StringPool[f.Strings.ValueIndex(i)];
        return null;
    }

    /// <summary>The value of an Int32 flat.</summary>
    public int? Int(ulong flat)
    {
        foreach (var f in files)
            if (f.Ints.Find(flat) is var i and >= 0) return f.Ints.Int(i);
        return null;
    }

    /// <summary>The record ids of an array:TweakDBID flat (for example a perk's requiresPerks); null if the flat is not there.</summary>
    public ulong[]? Links(ulong flat)
    {
        foreach (var f in files)
            if (f.Arrays.Find(flat) is var i and >= 0) return f.Arrays.Ids(f.ArrayAt[f.Arrays.ValueIndex(i)]);
        return null;
    }

    static string[] ReadStrings(byte[] block)
    {
        // Values: u32 count, then that many length-prefixed strings.
        var pool = new string[BinaryPrimitives.ReadUInt32LittleEndian(block)];
        for (int i = 0, p = CountSize; i < pool.Length; i++)
        {
            var length = OnScreens.ReadLength(block, ref p);
            pool[i] = Encoding.UTF8.GetString(block, p, length);
            p += length;
        }
        return pool;
    }

    // The arrays are one after another, each a varint count (the string length encoding) and that many 8-byte ids, so finding one means walking the ones before it.
    static int[] ArrayOffsets(byte[] block)
    {
        if (block.Length < CountSize) return [];
        var at = new int[BinaryPrimitives.ReadUInt32LittleEndian(block)];
        for (int i = 0, p = CountSize; i < at.Length; i++)
        {
            at[i] = p;
            var count = OnScreens.ReadLength(block, ref p);
            p += count * ValueSize;
        }
        return at;
    }

    static Tables? Load(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> head = stackalloc byte[HeaderSize];
        fs.ReadExactly(head);
        if (BinaryPrimitives.ReadUInt32LittleEndian(head) != Magic || BinaryPrimitives.ReadUInt32LittleEndian(head[VersionOffset..]) != SupportedVersion) return null;
        var recordsAt = BinaryPrimitives.ReadUInt32LittleEndian(head[RecordsOffsetField..]);

        var raw = new byte[BinaryPrimitives.ReadUInt32LittleEndian(head[TypeCountOffset..]) * TypeEntrySize];
        fs.ReadExactly(raw);
        var types = new List<(ulong Hash, int Keys, uint At)>();
        for (var i = 0; i < raw.Length; i += TypeEntrySize)
            types.Add((BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(i)), (int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i + TypeKeyCountOffset)), BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i + TypeDataOffsetOffset))));
        types.Sort((a, b) => a.At.CompareTo(b.At));

        FlatTable Read(ulong type, out byte[] block)
        {
            var i = types.FindIndex(t => t.Hash == type);
            if (i < 0) { block = []; return new FlatTable(block, 0); }
            var end = i + 1 < types.Count ? types[i + 1].At : recordsAt;
            fs.Position = types[i].At;
            block = new byte[end - types[i].At];
            fs.ReadExactly(block);
            return new FlatTable(block, types[i].Keys);
        }

        var locKeys = Read(LocKeyType, out _);
        var links = Read(TweakDbIdType, out _);
        var resources = Read(ResourceType, out _);
        var cnames = Read(CNameType, out var cnameBlock);

        var strings = Read(StringType, out var stringBlock);
        var ints = Read(Int32Type, out _);
        var arrays = Read(LinkArrayType, out var arrayBlock);
        return new Tables(locKeys, links, resources, cnames, ReadStrings(cnameBlock), strings, ReadStrings(stringBlock), ints, arrays, ArrayOffsets(arrayBlock));
    }

    static readonly ulong LocKeyType = Fnv("gamedataLocKeyWrapper"), TweakDbIdType = Fnv("TweakDBID"), ResourceType = Fnv("raRef:CResource"), CNameType = Fnv("CName"), StringType = Fnv("String"), Int32Type = Fnv("Int32"), LinkArrayType = Fnv("array:TweakDBID");

    static ulong Fnv(string s) => Hashing.Fnv1a64(s);

    /// <summary>One flat type's data block: the values, then the keys (sorted by name hash) at the end.</summary>
    sealed class FlatTable(byte[] block, int keyCount)
    {
        // A key: u32 name hash, u8 name length (at 4), 3 padding bytes, u32 index into the values (at 8).
        const int KeySize = 12, KeyLengthOffset = 4, KeyValueOffset = 8;

        readonly int keysAt = block.Length - keyCount * KeySize;

        /// <summary>The key's position, or -1.</summary>
        public int Find(ulong flat)
        {
            uint hash = (uint)flat, length = (uint)(flat >> 32);
            int lo = 0, hi = keyCount - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) >>> 1;
                var h = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(keysAt + mid * KeySize));
                if (h < hash) lo = mid + 1;
                else if (h > hash) hi = mid - 1;
                else
                {
                    // Different names can share a hash; the length tells them apart. Look at the neighbours with the same hash.
                    for (var i = mid; i >= 0 && H(i) == hash; i--) if (block[keysAt + i * KeySize + KeyLengthOffset] == length) return i;
                    for (var i = mid + 1; i < keyCount && H(i) == hash; i++) if (block[keysAt + i * KeySize + KeyLengthOffset] == length) return i;
                    return -1;
                }
            }
            return -1;
        }

        uint H(int i) => BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(keysAt + i * KeySize));

        public int ValueIndex(int key) => (int)BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(keysAt + key * KeySize + KeyValueOffset));

        // The values start after the u32 value count.
        int At(int key, int offset) => CountSize + ValueIndex(key) * ValueSize + offset;

        public uint U32(int key, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(At(key, offset)));

        public byte Byte(int key, int offset) => block[At(key, offset)];

        public int Int(int key) => BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(CountSize + ValueIndex(key) * sizeof(int)));

        /// <summary>The ids of the array starting at <paramref name="at"/>.</summary>
        public ulong[] Ids(int at)
        {
            var p = at;
            var ids = new ulong[OnScreens.ReadLength(block, ref p)];
            for (var i = 0; i < ids.Length; i++) ids[i] = TweakDb.Id(BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(p + i * ValueSize)), block[p + i * ValueSize + LinkLengthOffset]);
            return ids;
        }

        public ulong U64(int key) => BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(At(key, 0)));
    }
}
