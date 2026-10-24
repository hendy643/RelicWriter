namespace RelicWriter.Common.Container;

/// <summary>Positions and sizes in a sav.dat that the reader and writer share (see docs/save-format.md).</summary>
internal static class SaveLayout
{
    // Header
    public const int SaveVersionOffset = 4, GameVersionOffset = 8, ZeroByteOffset = 12, ArchiveVersionOffset = 21;
    /// <summary>Bytes 0..24 are kept verbatim as <see cref="SaveHeader.Prefix"/>; the chunk tag follows.</summary>
    public const int PrefixLength = 25;
    public const int ChunkTagOffset = PrefixLength, ChunkCountOffset = PrefixLength + TagLength;
    public const int TagLength = 4;

    // Chunk table: 12-byte slots (offset, stored size, raw size) starting right after the chunk count.
    public const int ChunkTableStart = 33, ChunkSlotSize = 12, ChunkSizeOffset = 4, ChunkRawSizeOffset = 8;
    /// <summary>The raw size of every chunk but the last.</summary>
    public const int ChunkSize = 262144;
    /// <summary>A chunk starts with its tag and raw size, then the LZ4 data; the stored size counts all of it.</summary>
    public const int ChunkHeaderSize = TagLength + 4;

    // Node table rows: name, then next sibling, first child, offset, size (u32 each).
    public const int RowNextOffset = 0, RowChildOffset = 4, RowOffsetOffset = 8, RowSizeOffset = 12, RowSize = 16;
    /// <summary>The "no such node" value in the next and child columns.</summary>
    public const int NoNode = -1;
    /// <summary>The u32 that starts every node's payload.</summary>
    public const int NodeIdSize = 4;

    // Trailer: the node table's offset, then the end tag.
    public const int TrailerOffsetSize = 4;
    public const int TrailerSize = TrailerOffsetSize + TagLength;

    public static ReadOnlySpan<byte> Magic => "VASC"u8;
    public static ReadOnlySpan<byte> ChunkTableTag => "FZLC"u8;
    public static ReadOnlySpan<byte> ChunkTag => "4ZLX"u8;
    public static ReadOnlySpan<byte> NodeTableTag => "EDON"u8;
    public static ReadOnlySpan<byte> EndTag => "ENOD"u8;
}
