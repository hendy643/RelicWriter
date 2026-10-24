using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace RelicWriter.Common.Container;

internal static class SaveReader
{
    public static SaveFile Read(byte[] file)
    {
        ReadOnlySpan<byte> f = file;
        if (f.Length < SaveLayout.ChunkTableStart || !f[..SaveLayout.TagLength].SequenceEqual(SaveLayout.Magic)) throw new InvalidDataException("Not a sav.dat (missing VASC).");
        if (f[SaveLayout.ZeroByteOffset] != 0 || !f[SaveLayout.ChunkTagOffset..SaveLayout.ChunkCountOffset].SequenceEqual(SaveLayout.ChunkTableTag)) throw new InvalidDataException("Unsupported header layout.");

        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(f[SaveLayout.ChunkCountOffset..]);
        var entries = new (int Offset, int Size, int RawSize)[count];
        for (var i = 0; i < count; i++)
        {
            var e = f[(SaveLayout.ChunkTableStart + SaveLayout.ChunkSlotSize * i)..];
            entries[i] = ((int)BinaryPrimitives.ReadUInt32LittleEndian(e), (int)BinaryPrimitives.ReadUInt32LittleEndian(e[SaveLayout.ChunkSizeOffset..]), (int)BinaryPrimitives.ReadUInt32LittleEndian(e[SaveLayout.ChunkRawSizeOffset..]));
        }

        var first = entries[0].Offset;
        var data = new byte[entries.Sum(e => e.RawSize)];
        var pos = 0;
        foreach (var (offset, size, rawSize) in entries)
        {
            if (!f.Slice(offset, SaveLayout.TagLength).SequenceEqual(SaveLayout.ChunkTag)) throw new InvalidDataException($"Bad chunk tag at {offset}.");
            if (LZ4Codec.Decode(f.Slice(offset + SaveLayout.ChunkHeaderSize, size - SaveLayout.ChunkHeaderSize), data.AsSpan(pos, rawSize)) != rawSize) throw new InvalidDataException($"Chunk at {offset} did not decompress to {rawSize} bytes.");
            pos += rawSize;
        }

        if (!f[^SaveLayout.TagLength..].SequenceEqual(SaveLayout.EndTag)) throw new InvalidDataException("Missing node table trailer.");
        var p = (int)BinaryPrimitives.ReadUInt32LittleEndian(f[^SaveLayout.TrailerSize..]);
        if (!f.Slice(p, SaveLayout.TagLength).SequenceEqual(SaveLayout.NodeTableTag)) throw new InvalidDataException("Bad node table tag.");
        p += SaveLayout.TagLength;
        var n = SaveBinary.ReadVarint(f, ref p);
        var table = new (string Name, int Next, int Child, int Offset, int Size)[n];
        for (var i = 0; i < n; i++)
        {
            var name = SaveBinary.ReadName(f, ref p);
            table[i] = (name, BinaryPrimitives.ReadInt32LittleEndian(f[(p + SaveLayout.RowNextOffset)..]), BinaryPrimitives.ReadInt32LittleEndian(f[(p + SaveLayout.RowChildOffset)..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(f[(p + SaveLayout.RowOffsetOffset)..]) - first, (int)BinaryPrimitives.ReadUInt32LittleEndian(f[(p + SaveLayout.RowSizeOffset)..]));
            p += SaveLayout.RowSize;
        }

        var save = new SaveFile
        {
            Header = new SaveHeader(BinaryPrimitives.ReadUInt32LittleEndian(f[SaveLayout.SaveVersionOffset..]), BinaryPrimitives.ReadUInt32LittleEndian(f[SaveLayout.GameVersionOffset..]), BinaryPrimitives.ReadUInt32LittleEndian(f[SaveLayout.ArchiveVersionOffset..]),
                f[..SaveLayout.PrefixLength].ToArray(), (first - SaveLayout.ChunkTableStart) / SaveLayout.ChunkSlotSize),
        };

        int LastSibling(int index)
        {
            while (table[index].Next != SaveLayout.NoNode) index = table[index].Next;
            return index;
        }

        List<SaveNode> Siblings(int index)
        {
            var list = new List<SaveNode>();
            for (; index != SaveLayout.NoNode; index = table[index].Next)
            {
                var (name, _, child, off, size) = table[index];
                var node = new SaveNode { Name = name, Id = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off)) };
                var end = off + size;
                var leadEnd = end;
                if (child != SaveLayout.NoNode)
                {
                    node.Children.AddRange(Siblings(child));
                    leadEnd = table[child].Offset;
                    var last = table[LastSibling(child)];
                    node.Trailing = data.AsSpan(last.Offset + last.Size, end - (last.Offset + last.Size)).ToArray();
                }
                node.Data = data.AsSpan(off + SaveLayout.NodeIdSize, leadEnd - (off + SaveLayout.NodeIdSize)).ToArray();
                var next = table[index].Next;
                if (next != SaveLayout.NoNode) node.After = data.AsSpan(end, table[next].Offset - end).ToArray();
                list.Add(node);
            }
            return list;
        }

        if (n > 0) save.Roots.AddRange(Siblings(0));
        return save;
    }
}
