using RelicWriter.Common;
using System.Buffers.Binary;
using System.Text;

namespace RelicWriter.GameAssets;

/// <summary>
/// One of the game's .archive files (format "RDAR", version 12). Files are keyed by the FNV-1a 64-bit hash of their lower-case path with
/// backslashes; the archive stores no names. A file is a list of segments: the first holds the file itself, later ones hold data buffers
/// (a texture's pixels, for example). The index is read the first time it is needed.
/// </summary>
public sealed class GameArchive(string path)
{
    // Archive header (40 bytes): magic, version, then where the index is and how big.
    const uint Magic = 0x52414452, SupportedVersion = 12; // "RDAR"
    const int HeaderSize = 40, VersionOffset = 4, IndexPositionOffset = 8, IndexSizeOffset = 16;
    // Index: a 28-byte header (file count at 16), 56-byte file entries (hash, then at 20 and 24 the first and last segment), 16-byte segments.
    const int FileEntrySize = 56, SegmentSize = 16, IndexHeaderSize = 28, FileCountOffset = 16, FirstSegmentInEntry = 20, LastSegmentInEntry = 24;
    // A segment: u64 offset, u32 stored size, u32 real size.
    const int SegmentStoredSizeOffset = 8, SegmentRealSizeOffset = 12;
    // A compressed segment is "KARK" (as a u32), the real size, then the Oodle stream.
    const uint CompressedMagic = 0x4B52414B;
    const int CompressedHeaderSize = 8;

    byte[]? index;
    Dictionary<ulong, int>? files;

    public static ulong PathHash(string gamePath)
    {
        return Hashing.Fnv1a64(Encoding.UTF8.GetBytes(gamePath.ToLowerInvariant().Replace('/', '\\')));
    }

    public bool Contains(ulong hash) => Files.ContainsKey(hash);

    /// <summary>The bytes of one segment of the file, decompressed; null if the file isn't here or can't be decompressed.</summary>
    public byte[]? Read(ulong hash, int segment = 0)
    {
        if (!Files.TryGetValue(hash, out var entry)) return null;
        var first = (int)BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(entry + FirstSegmentInEntry));
        var last = (int)BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(entry + LastSegmentInEntry));
        if (segment < 0 || first + segment >= last) return null;

        var seg = IndexHeaderSize + FileCount * FileEntrySize + (first + segment) * SegmentSize;
        var offset = BinaryPrimitives.ReadInt64LittleEndian(index.AsSpan(seg));
        var stored = (int)BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(seg + SegmentStoredSizeOffset));
        var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(seg + SegmentRealSizeOffset));

        using var fs = File.OpenRead(path);
        fs.Position = offset;
        var data = new byte[stored];
        fs.ReadExactly(data);
        if (stored == size) return data;

        // Compressed: "KARK", the real size, then an Oodle (Kraken) stream.
        if (BinaryPrimitives.ReadUInt32LittleEndian(data) != CompressedMagic) return null;
        var raw = new byte[size];
        return Oodle.Decompress(data.AsSpan(CompressedHeaderSize), raw) ? raw : null;
    }

    int FileCount => (int)BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(FileCountOffset));

    Dictionary<ulong, int> Files
    {
        get
        {
            if (files is not null) return files;
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[HeaderSize];
            fs.ReadExactly(header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic || BinaryPrimitives.ReadUInt32LittleEndian(header[VersionOffset..]) != SupportedVersion)
                throw new InvalidDataException($"{path} is not an RDAR version {SupportedVersion} archive.");
            fs.Position = BinaryPrimitives.ReadInt64LittleEndian(header[IndexPositionOffset..]);
            index = new byte[BinaryPrimitives.ReadUInt32LittleEndian(header[IndexSizeOffset..])];
            fs.ReadExactly(index);

            var table = new Dictionary<ulong, int>(FileCount);
            for (var i = 0; i < FileCount; i++)
            {
                var entry = IndexHeaderSize + i * FileEntrySize;
                table[BinaryPrimitives.ReadUInt64LittleEndian(index.AsSpan(entry))] = entry;
            }
            return files = table;
        }
    }
}

/// <summary>Several archives searched in order, so a Phantom Liberty file replaces the base game's.</summary>
public sealed class GameFiles(IEnumerable<GameArchive> archives)
{
    readonly List<GameArchive> archives = archives.ToList();

    public byte[]? Read(string gamePath, int segment = 0) => Read(GameArchive.PathHash(gamePath), segment);

    public byte[]? Read(ulong hash, int segment = 0)
    {
        foreach (var archive in archives)
            if (archive.Contains(hash)) return archive.Read(hash, segment);
        return null;
    }
}
