using System.Buffers.Binary;
using RelicWriter.Common.Container;

namespace RelicWriter.Common.Model;

/// <summary>Which optional parts an item's node has (see docs/save-node-formats.md): a stack has a quantity and a mod tree, "other" only a quantity, gear only a mod tree.</summary>
public enum ItemShape { Stack, Other, Gear }

/// <summary>One item in an inventory. The record is a TweakDBID (CRC-32 of the record name plus its length); names are not stored in the save.</summary>
public sealed class ItemEntry
{
    internal SaveNode Node { get; init; } = null!;
    internal int QuantityOffset { get; init; } = -1; // -1: the item has no quantity

    public required uint RecordHash { get; init; }
    public required byte RecordLength { get; init; }
    public required uint Seed { get; init; }
    public ItemShape Shape { get; init; }

    /// <summary>The size of the item's node data in bytes, for bug reports.</summary>
    public int NodeBytes => Node.Data.Length;

    /// <summary>The stack size, or null for items that carry no quantity (kind 2: gear with mods).</summary>
    public uint? Quantity { get; set; }

    public bool IsMoney => RecordHash == Inventory.MoneyHash && RecordLength == Inventory.MoneyRecord.Length;

    /// <summary>The record in the form the game's data uses for ids that have no known name.</summary>
    public string RecordId => $"{RecordHash:x8}/{RecordLength}";
}

/// <summary>One change made through <see cref="Inventory"/> since the save was opened, kept for bug reports. An add also names the item it was copied from.</summary>
public sealed record InventoryEdit(string Action, uint Hash, byte Length, ItemShape Shape, uint? DonorHash, byte? DonorLength, int? DonorBytes, int Bytes);

public sealed class SubInventory
{
    /// <summary>The sub-inventory id (1 is the first, 1,000,000 the second; what each one is, is not known).</summary>
    public required ulong Id { get; init; }

    public List<ItemEntry> Items { get; } = [];

    // Where the record's item count is: the index of the inventory's byte segment holding it (-1: unknown) and the offset in it.
    internal int CountSegment { get; init; }
    internal int CountOffset { get; init; }
}

/// <summary>The save's inventory node: sub-inventories of items, with the stack size of items that have one editable.</summary>
public sealed class Inventory
{
    /// <summary>The record of the money item; its TweakDBID is the CRC-32 of this name plus its length.</summary>
    public const string MoneyRecord = "Items.money";

    public static readonly uint MoneyHash = Crc32("Items.money"u8);

    // An item's id: record hash (u32) and length (u8) in 8 bytes, the seed (u32) at 8, then optional parts that depend on the save version.
    const int RecordLengthOffset = 4;
    const int SeedOffset = 8, IdBaseSize = 12, KindByteOffset = 12, KindByteSize = 1, ExtraBytesSize = 2, TrailingByteSize = 1;
    /// <summary>After the item id come a flags byte and an unknown u32; the quantity follows.</summary>
    const int FlagsByteSize = 1;
    // The kind byte: 1 is gear with mods, 2 something between, 3 a stack. Saves before it existed are told apart by the seed.
    const byte KindByteGear = 1, KindByteOther = 2, KindByteStack = 3;
    const int KindStack = 0, KindOther = 1, KindGear = 2;
    const uint SeedOfOtherKind = 2;
    const int NoQuantity = -1;
    /// <summary>From this save version an ItemMod ends in a 16-byte trailer.</summary>
    const uint ModTrailerVersion = 192;
    // A sub-inventory record: u64 id, u32 item count.
    const int SubInventoryCountOffset = sizeof(ulong), SubInventorySize = sizeof(ulong) + sizeof(uint);

    /// <summary>The sub-inventory id of V's bag, where <see cref="Add"/> puts items.</summary>
    public const ulong BagId = 1;

    readonly SaveFile file;
    readonly SaveNode node;
    readonly int idSize;

    Inventory(SaveFile file, SaveNode node)
    {
        this.file = file;
        this.node = node;
        var v = file.Header.SaveVersion;
        idSize = IdBaseSize + (v >= SaveVersions.ItemKindByte ? KindByteSize : 0) + (v >= SaveVersions.ItemExtraBytes ? ExtraBytesSize : 0) + (v >= SaveVersions.ItemTrailingByte ? TrailingByteSize : 0);
    }

    public List<SubInventory> SubInventories { get; } = [];

    /// <summary>What was added and deleted since the save was opened, oldest first.</summary>
    public List<InventoryEdit> Edits { get; } = [];

    /// <summary>The mod tree of a gear or stack item, or null for the other shape or when the tree is not in a layout this reads exactly.</summary>
    public ItemModSummary? ModTree(ItemEntry item) =>
        item.Shape == ItemShape.Other ? null : ItemModTree.Read(item.Node.Data, ItemModTree.Start(idSize, item.Shape == ItemShape.Stack), idSize, file.Header.SaveVersion >= ModTrailerVersion);

    public ItemEntry? Money => SubInventories.SelectMany(s => s.Items).FirstOrDefault(i => i.IsMoney);

    public static Inventory? Read(SaveFile file)
    {
        if (file.Walk().FirstOrDefault(n => n.Name == "inventory") is not { } node) return null;
        var inventory = new Inventory(file, node);
        inventory.Parse();
        return inventory;
    }

    void Parse()
    {
        var v = file.Header.SaveVersion;
        var items = node.Children.Where(c => c.Name == "itemData").Select(c => ReadItem(c, v, idSize)).ToList();
        SubInventories.Clear();
        try
        {
            // Sub-inventory records and item id headers sit in the inventory's own bytes, in the gaps after each child and in the trailing bytes.
            var segments = Segments();
            var seg = 0;
            var pos = 0;
            var next = 0;
            var subCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(node.Data);
            pos = sizeof(uint);
            for (var s = 0; s < subCount; s++)
            {
                var sub = new SubInventory { Id = BinaryPrimitives.ReadUInt64LittleEndian(segments[seg].AsSpan(pos)), CountSegment = seg, CountOffset = pos + SubInventoryCountOffset };
                var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(segments[seg].AsSpan(sub.CountOffset));
                pos += SubInventorySize;
                for (var i = 0; i < count; i++)
                {
                    sub.Items.Add(items[next++]); // the item id header at pos is not needed; the child repeats it
                    seg++;
                    pos = 0;
                }
                SubInventories.Add(sub);
            }
            if (next != items.Count) throw new InvalidDataException("Item counts do not add up.");
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            // shortcut: if the sub-inventory records do not parse, all items are shown in one group with id 0 and nothing can be added.
            SubInventories.Clear();
            var all = new SubInventory { Id = 0, CountSegment = -1 };
            all.Items.AddRange(items);
            SubInventories.Add(all);
        }
    }

    // The inventory's own bytes in file order: the data, the gap after every child but the last, then the trailing bytes (sub-inventory records after the last item).
    List<byte[]> Segments() => [node.Data, .. node.Children.Take(node.Children.Count - 1).Select(c => c.After), node.Trailing];

    /// <summary>
    /// Puts a new item in V's bag by copying the plainest item of the same shape already in the save (a node with an empty mod tree), then giving the copy the new
    /// record. The sub-inventory count, the item id header in front of the copy and the node numbering are kept in step, and the inventory is read again, so
    /// entries obtained before the call must be dropped. Quantity edits made so far are kept. Returns null when the save cannot take it: no bag, or no item
    /// of that shape to copy from. Not checked in the game itself.
    /// </summary>
    public ItemEntry? Add(uint hash, byte length, ItemShape shape, uint quantity)
    {
        if (RecordNames.IsQuestItem(hash, length)) return null;
        var bag = SubInventories.FirstOrDefault(s => s.Id == BagId);
        if (bag is null || bag.CountSegment < 0 || bag.Items.Count == 0) return null;
        var template = SubInventories.SelectMany(s => s.Items).Where(i => i.Shape == shape && !i.IsMoney).MinBy(i => i.Node.Data.Length);
        if (template is null) return null;
        ApplyTo();

        var data = (byte[])template.Node.Data.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(data, hash);
        data[RecordLengthOffset] = length;
        if (shape == ItemShape.Gear) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(SeedOffset), (uint)Random.Shared.NextInt64(SeedOfOtherKind + 1, uint.MaxValue));
        if (shape != ItemShape.Gear) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(idSize + FlagsByteSize + sizeof(uint)), quantity);
        var added = new SaveNode { Name = template.Node.Name, Data = data };

        // Node ids are the nodes' positions in the file (true in every save checked); keep it that way.
        var sequential = file.Walk().Select((n, i) => n.Id == (uint)i).All(x => x);
        var segments = Segments();
        var count = segments[bag.CountSegment].AsSpan(bag.CountOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(count, BinaryPrimitives.ReadUInt32LittleEndian(count) + 1);

        // The node goes after the bag's last item; that item's gap (the next sub-inventory record and the next item's id header) moves behind it, and the gap now holds the new item's header.
        var last = bag.Items[^1].Node;
        added.After = last.After;
        last.After = data[..idSize];
        node.Children.Insert(node.Children.IndexOf(last) + 1, added);

        if (sequential) foreach (var (n, i) in file.Walk().Select((n, i) => (n, i))) n.Id = (uint)i;
        else added.Id = file.Walk().Max(n => n.Id) + 1;

        Edits.Add(new("add", hash, length, shape, template.RecordHash, template.RecordLength, template.Node.Data.Length, data.Length));
        Parse();
        return SubInventories.First(s => s.Id == BagId).Items.First(i => i.Node == added);
    }

    /// <summary>
    /// Deletes an item: the reverse of <see cref="Add"/>. The item's node and the id header in front of it go, the sub-inventory count drops by one and the node
    /// numbering is kept in step; the inventory is read again, so entries obtained before the call must be dropped. Returns false for eddies, quest items, for a save whose
    /// sub-inventory records did not parse, and for the very last node of the inventory (its trailing bytes are not understood). Not checked in the game itself.
    /// </summary>
    public bool Remove(ItemEntry item)
    {
        var sub = SubInventories.FirstOrDefault(s => s.Items.Contains(item));
        var c = node.Children.IndexOf(item.Node);
        if (sub is null || sub.CountSegment < 0 || item.IsMoney || RecordNames.IsQuestItem(item.RecordHash, item.RecordLength) || c < 0 || c == node.Children.Count - 1) return false;
        ApplyTo();

        Edits.Add(new("remove", item.RecordHash, item.RecordLength, item.Shape, null, null, null, item.Node.Data.Length));
        var sequential = file.Walk().Select((n, i) => n.Id == (uint)i).All(x => x);
        var segments = Segments();
        var count = segments[sub.CountSegment].AsSpan(sub.CountOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(count, BinaryPrimitives.ReadUInt32LittleEndian(count) - 1);

        // The gap in front of the item ends in its id header; drop that and join the item's own gap on.
        var gap = segments[c][..^idSize].Concat(item.Node.After).ToArray();
        if (c == 0) node.Data = gap;
        else node.Children[c - 1].After = gap;
        node.Children.RemoveAt(c);

        if (sequential) foreach (var (n, i) in file.Walk().Select((n, i) => (n, i))) n.Id = (uint)i;
        Parse();
        return true;
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
            Shape = kind switch { KindStack => ItemShape.Stack, KindOther => ItemShape.Other, _ => ItemShape.Gear },
            Quantity = kind != KindGear ? BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(qty)) : null,
            QuantityOffset = kind != KindGear ? qty : NoQuantity,
        };
    }

    internal static uint Crc32(ReadOnlySpan<byte> data) => Hashing.Crc32(data);
}
