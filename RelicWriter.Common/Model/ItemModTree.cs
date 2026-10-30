using System.Buffers.Binary;
using RelicWriter.Common.Container;

namespace RelicWriter.Common.Model;

/// <summary>What an item's mod tree holds: how many mods hang off the root, how deep, and the records of the mods and of the slots they sit in (see docs/save-node-formats.md, ItemMod).</summary>
public sealed record ItemModSummary(uint RootHash, byte RootLength, int Mods, int Depth, IReadOnlyList<(uint Hash, byte Length)> ModRecords, IReadOnlyList<(uint Hash, byte Length)> SlotRecords);

static class ItemModTree
{
    const int RootHeaderSize = 16, TrailerSize = 16, TrailingU32Size = sizeof(uint), RecordSize = 8, FlagsSize = 1, UnknownSize = sizeof(uint), MaxDepth = 16;

    /// <summary>Reads the mod tree that starts at <paramref name="start"/>; null unless it uses the item's bytes exactly, so a layout this does not know is never half-read.</summary>
    public static ItemModSummary? Read(byte[] data, int start, int idSize, bool hasTrailer)
    {
        var mods = new List<(uint, byte)>();
        var slots = new List<(uint, byte)>();
        var p = start;
        try
        {
            var depth = 0;
            var root = ((uint)0, (byte)0);
            var count = 0;
            void Walk(int level, bool isRoot)
            {
                if (level > MaxDepth) throw new InvalidDataException();
                depth = Math.Max(depth, level);
                count++;
                var hash = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p));
                var length = data[p + sizeof(uint)];
                p += idSize;
                if (isRoot) root = (hash, length); else mods.Add((hash, length));
                SaveBinary.ReadName(data, ref p);
                if (!isRoot) slots.Add((BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p)), data[p + sizeof(uint)]));
                p += RecordSize;
                var children = SaveBinary.ReadVarint(data, ref p);
                if (children is < 0 or > 1000) throw new InvalidDataException();
                for (var i = 0; i < children; i++) Walk(level + 1, false);
                p += TrailingU32Size + (hasTrailer ? TrailerSize : 0);
            }
            Walk(0, true);
            return p == data.Length ? new ItemModSummary(root.Item1, root.Item2, count - 1, depth, mods, slots) : null;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Where the mod tree starts in an item that has one: after the item id, flags, an unknown u32, the quantity if there is one, and the 16-byte root header.</summary>
    public static int Start(int idSize, bool hasQuantity) => idSize + FlagsSize + UnknownSize + (hasQuantity ? sizeof(uint) : 0) + RootHeaderSize;
}
