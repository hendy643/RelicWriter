using System.Buffers.Binary;
using RelicWriter.Common.Container;

namespace RelicWriter.Common.Model;

/// <summary>One item in an inventory. The record is a TweakDBID (CRC-32 of the record name plus its length); names are not stored in the save.</summary>
public sealed class ItemEntry
{
    internal SaveNode Node { get; init; } = null!;
    internal int QuantityOffset { get; init; } = -1; // -1: the item has no quantity

    public required uint RecordHash { get; init; }
    public required byte RecordLength { get; init; }
    public required uint Seed { get; init; }

    /// <summary>The stack size, or null for items that carry no quantity (kind 2: gear with mods).</summary>
    public uint? Quantity { get; set; }

    public bool IsMoney => RecordHash == Inventory.MoneyHash && RecordLength == Inventory.MoneyRecord.Length;

    /// <summary>The record in the form the game's data uses for ids that have no known name.</summary>
    public string RecordId => $"{RecordHash:x8}/{RecordLength}";
}

public sealed class SubInventory
{
    /// <summary>The sub-inventory id (1 is the first, 1,000,000 the second; what each one is, is not known).</summary>
    public required ulong Id { get; init; }

    public List<ItemEntry> Items { get; } = [];
}

/// <summary>The save's inventory node: sub-inventories of items, with the stack size of items that have one editable.</summary>
public sealed class Inventory
{
    /// <summary>The record of the money item; its TweakDBID is the CRC-32 of this name plus its length.</summary>
    public const string MoneyRecord = "Items.money";

    public static readonly uint MoneyHash = Crc32("Items.money"u8);

    // An item's id: record hash (u32) and length (u8) in 8 bytes, the seed (u32) at 8, then optional parts that depend on the save version.
    const int SeedOffset = 8, IdBaseSize = 12, KindByteOffset = 12, KindByteSize = 1, ExtraBytesSize = 2, TrailingByteSize = 1;
    /// <summary>After the item id come a flags byte and an unknown u32; the quantity follows.</summary>
    const int FlagsByteSize = 1;
    // The kind byte: 1 is gear with mods, 2 something between, 3 a stack. Saves before it existed are told apart by the seed.
    const byte KindByteGear = 1, KindByteOther = 2, KindByteStack = 3;
    const int KindStack = 0, KindOther = 1, KindGear = 2;
    const uint SeedOfOtherKind = 2;
    const int NoQuantity = -1;
    // A sub-inventory record: u64 id, u32 item count.
    const int SubInventoryCountOffset = sizeof(ulong), SubInventorySize = sizeof(ulong) + sizeof(uint);

    public List<SubInventory> SubInventories { get; } = [];

    public ItemEntry? Money => SubInventories.SelectMany(s => s.Items).FirstOrDefault(i => i.IsMoney);

    public static Inventory? Read(SaveFile file)
    {
        var node = file.Walk().FirstOrDefault(n => n.Name == "inventory");
        if (node is null) return null;
        var v = file.Header.SaveVersion;
        var idSize = IdBaseSize + (v >= SaveVersions.ItemKindByte ? KindByteSize : 0) + (v >= SaveVersions.ItemExtraBytes ? ExtraBytesSize : 0) + (v >= SaveVersions.ItemTrailingByte ? TrailingByteSize : 0);
        var items = node.Children.Where(c => c.Name == "itemData").Select(c => ReadItem(c, v, idSize)).ToList();

        var inventory = new Inventory();
        try
        {
            // Sub-inventory records and item id headers sit in the inventory's own bytes, in the gaps after each child and in the trailing bytes.
            var segments = new List<byte[]> { node.Data };
            segments.AddRange(node.Children.Take(node.Children.Count - 1).Select(c => c.After));
            segments.Add(node.Trailing); // sub-inventory records that come after the last item
            var seg = 0;
            var pos = 0;
            var next = 0;
            var subCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(node.Data);
            pos = sizeof(uint);
            for (var s = 0; s < subCount; s++)
            {
                var sub = new SubInventory { Id = BinaryPrimitives.ReadUInt64LittleEndian(segments[seg].AsSpan(pos)) };
                var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(segments[seg].AsSpan(pos + SubInventoryCountOffset));
                pos += SubInventorySize;
                for (var i = 0; i < count; i++)
                {
                    sub.Items.Add(items[next++]); // the item id header at pos is not needed; the child repeats it
                    seg++;
                    pos = 0;
                }
                inventory.SubInventories.Add(sub);
            }
            if (next != items.Count) throw new InvalidDataException("Item counts do not add up.");
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            // shortcut: if the sub-inventory records do not parse, all items are shown in one group with id 0.
            inventory.SubInventories.Clear();
            var all = new SubInventory { Id = 0 };
            all.Items.AddRange(items);
            inventory.SubInventories.Add(all);
        }
        return inventory;
    }

    public void ApplyTo()
    {
        foreach (var item in SubInventories.SelectMany(s => s.Items))
            if (item.Quantity is { } q && item.QuantityOffset >= 0)
                BinaryPrimitives.WriteUInt32LittleEndian(item.Node.Data.AsSpan(item.QuantityOffset), q);
    }

    static ItemEntry ReadItem(SaveNode n, uint v, int idSize)
    {
        var d = n.Data;
        var seed = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(SeedOffset));
        var kindByte = v >= SaveVersions.ItemKindByte ? d[KindByteOffset] : (byte)0;
        var kind = kindByte switch { KindByteGear => KindGear, KindByteOther => KindOther, KindByteStack => KindStack, _ => seed != SeedOfOtherKind ? KindGear : KindOther };
        var qty = idSize + FlagsByteSize + sizeof(uint);
        return new ItemEntry
        {
            Node = n,
            RecordHash = BinaryPrimitives.ReadUInt32LittleEndian(d),
            RecordLength = d[4],
            Seed = seed,
            Quantity = kind != KindGear ? BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(qty)) : null,
            QuantityOffset = kind != KindGear ? qty : NoQuantity,
        };
    }

    internal static uint Crc32(ReadOnlySpan<byte> data) => Hashing.Crc32(data);
}
