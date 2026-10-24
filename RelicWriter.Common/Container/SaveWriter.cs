using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace RelicWriter.Common.Container;

internal static class SaveWriter
{
    public static byte[] Write(SaveFile save)
    {
        var h = save.Header;
        var first = SaveLayout.ChunkTableStart + SaveLayout.ChunkSlotSize * h.ChunkTableCapacity;

        // Node payloads, depth-first; each node's table row is completed once its size is known.
        var body = new MemoryStream();
        var rows = new List<(string Name, int Next, int Child, int Offset, int Size)>();
        void WriteSiblings(List<SaveNode> nodes)
        {
            var prev = SaveLayout.NoNode;
            foreach (var node in nodes)
            {
                var index = rows.Count;
                var start = (int)body.Length;
                rows.Add((node.Name, SaveLayout.NoNode, SaveLayout.NoNode, first + start, 0));
                if (prev != SaveLayout.NoNode) rows[prev] = rows[prev] with { Next = index };
                prev = index;

                WriteU32(body, node.Id);
                body.Write(node.Data);
                if (node.Children.Count > 0)
                {
                    rows[index] = rows[index] with { Child = rows.Count };
                    WriteSiblings(node.Children);
                }
                body.Write(node.Trailing);
                rows[index] = rows[index] with { Size = (int)body.Length - start };
                body.Write(node.After);
            }
        }
        WriteSiblings(save.Roots);

        var raw = body.GetBuffer().AsSpan(0, (int)body.Length);
        var chunkCount = (raw.Length + SaveLayout.ChunkSize - 1) / SaveLayout.ChunkSize;
        if (chunkCount > h.ChunkTableCapacity) throw new InvalidOperationException($"{chunkCount} chunks do not fit the {h.ChunkTableCapacity}-slot chunk table.");

        var o = new MemoryStream();
        o.Write(h.Prefix);
        o.Write(SaveLayout.ChunkTableTag);
        WriteU32(o, (uint)chunkCount);
        o.Write(new byte[first - (int)o.Length]); // chunk table slots, filled in below
        var table = new (int Offset, int Size, int RawSize)[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            var part = raw.Slice(i * SaveLayout.ChunkSize, Math.Min(SaveLayout.ChunkSize, raw.Length - i * SaveLayout.ChunkSize));
            var buf = new byte[LZ4Codec.MaximumOutputSize(part.Length)];
            var n = LZ4Codec.Encode(part, buf, LZ4Level.L00_FAST);
            table[i] = ((int)o.Length, n + SaveLayout.ChunkHeaderSize, part.Length);
            o.Write(SaveLayout.ChunkTag);
            WriteU32(o, (uint)part.Length);
            o.Write(buf, 0, n);
        }

        var tableOffset = (int)o.Length;
        o.Write(SaveLayout.NodeTableTag);
        SaveBinary.WriteVarint(o, rows.Count);
        foreach (var (name, next, child, offset, size) in rows)
        {
            SaveBinary.WriteName(o, name);
            WriteU32(o, (uint)next);
            WriteU32(o, (uint)child);
            WriteU32(o, (uint)offset);
            WriteU32(o, (uint)size);
        }
        WriteU32(o, (uint)tableOffset);
        o.Write(SaveLayout.EndTag);

        var result = o.ToArray();
        for (var i = 0; i < chunkCount; i++)
        {
            var e = result.AsSpan(SaveLayout.ChunkTableStart + SaveLayout.ChunkSlotSize * i);
            BinaryPrimitives.WriteUInt32LittleEndian(e, (uint)table[i].Offset);
            BinaryPrimitives.WriteUInt32LittleEndian(e[SaveLayout.ChunkSizeOffset..], (uint)table[i].Size);
            BinaryPrimitives.WriteUInt32LittleEndian(e[SaveLayout.ChunkRawSizeOffset..], (uint)table[i].RawSize);
        }
        return result;
    }

    static void WriteU32(Stream o, uint v)
    {
        Span<byte> b = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        o.Write(b);
    }
}
