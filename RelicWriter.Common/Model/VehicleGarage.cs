using System.Buffers.Binary;
using RelicWriter.Common.Container;

namespace RelicWriter.Common.Model;

/// <summary>A vehicle record as stored: the CRC-32 of its name plus the name's length (a TweakDBID).</summary>
public readonly record struct VehicleId(uint Hash, byte Length)
{
    public static VehicleId FromName(string name) => new(Inventory.Crc32(System.Text.Encoding.ASCII.GetBytes(name)), (byte)name.Length);
}

/// <summary>
/// The player's unlocked vehicles: the <c>unlockedVehicleArray</c> property of the vehicleGarageComponentPS entry in PersistencySystem2
/// (save version 205 and later). Only that array is rewritten; every other byte of the node is kept.
/// Saves before 205 keep this in PSData and are not read (Read returns null).
/// </summary>
public sealed class VehicleGarage
{
    // Property stream names/types are FNV-1a 64-bit hashes (docs/save-property-streams.md).
    static readonly ulong GarageClass = Fnv("vehicleGarageComponentPS"),
        ArrayName = Fnv("unlockedVehicleArray"), ArrayType = Fnv("array:vehicleUnlockedVehicle"),
        VehicleIdName = Fnv("vehicleID"), VehicleIdType = Fnv("vehicleGarageVehicleID"),
        RecordIdName = Fnv("recordID"), TweakDbId = Fnv("TweakDBID"), HealthName = Fnv("health");

    // An unlocked vehicle: name+type hashes of vehicleID, name+type of recordID, the TweakDBID (hash, length), padding and two list terminators.
    const int ElementSize = 56, HashSize = sizeof(ulong), PropertyHeaderSize = 2 * HashSize;
    const int RecordNameOffset = 2 * HashSize, RecordTypeOffset = 3 * HashSize, RecordHashOffset = 4 * HashSize, RecordLengthOffset = RecordHashOffset + sizeof(uint);
    // The entry's size field sits just before its data.
    const int SizeFieldSize = sizeof(uint);
    // An entry in the entry list: u64 id (0 for an empty one), u64 class hash, u32 size, then the data.
    const int EntryClassOffset = HashSize, EntrySizeOffset = 2 * HashSize, EntryHeaderSize = 2 * HashSize + SizeFieldSize;
    // A recordID property: name hash, type hash, then the TweakDBID (u32 hash, u8 length, 3 padding).
    const int RecordPropertySize = PropertyHeaderSize + sizeof(uint) + 4, RecordLengthInProperty = PropertyHeaderSize + sizeof(uint);
    const int HealthSize = PropertyHeaderSize + sizeof(float);

    readonly SaveNode node;
    readonly int entryStart, arrayStart, arrayEnd;
    readonly List<(VehicleId Id, byte[] Raw)> items;
    bool changed;

    VehicleGarage(SaveNode node, int entryStart, int arrayStart, int arrayEnd, List<(VehicleId, byte[])> items)
    {
        this.node = node;
        this.entryStart = entryStart;
        this.arrayStart = arrayStart;
        this.arrayEnd = arrayEnd;
        this.items = items;
    }

    public IEnumerable<VehicleId> Unlocked => items.Select(i => i.Id);

    public bool IsUnlocked(VehicleId id) => items.Any(i => i.Id == id);

    public void Unlock(VehicleId id)
    {
        if (IsUnlocked(id)) return;
        var raw = new byte[ElementSize];
        var s = raw.AsSpan();
        BinaryPrimitives.WriteUInt64LittleEndian(s, VehicleIdName);
        BinaryPrimitives.WriteUInt64LittleEndian(s[HashSize..], VehicleIdType);
        BinaryPrimitives.WriteUInt64LittleEndian(s[RecordNameOffset..], RecordIdName);
        BinaryPrimitives.WriteUInt64LittleEndian(s[RecordTypeOffset..], TweakDbId);
        BinaryPrimitives.WriteUInt32LittleEndian(s[RecordHashOffset..], id.Hash);
        s[RecordLengthOffset] = id.Length; // the last 3 bytes and both list terminators stay 0
        items.Add((id, raw));
        changed = true;
    }

    public void Lock(VehicleId id) => changed |= items.RemoveAll(i => i.Id == id) > 0;

    /// <summary>Writes the array back into the node. Does nothing when nothing changed, so an unedited save stays byte-identical.</summary>
    public void ApplyTo()
    {
        if (!changed) return;
        var old = node.Data;
        var body = items.SelectMany(i => i.Raw).ToArray();
        var data = new byte[old.Length - (arrayEnd - arrayStart) + sizeof(uint) + body.Length];
        old.AsSpan(0, arrayStart).CopyTo(data);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(arrayStart), (uint)items.Count);
        body.CopyTo(data.AsSpan(arrayStart + sizeof(uint)));
        old.AsSpan(arrayEnd).CopyTo(data.AsSpan(arrayStart + sizeof(uint) + body.Length));
        var size = BinaryPrimitives.ReadUInt32LittleEndian(old.AsSpan(entryStart - SizeFieldSize)) + (uint)(data.Length - old.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(entryStart - SizeFieldSize), size);
        node.Data = data;
        changed = false;
    }

    public static VehicleGarage? Read(SaveFile file)
    {
        var node = file.Walk().FirstOrDefault(n => n.Name == "PersistencySystem2");
        if (node is null) return null;
        try { return Parse(node); }
        catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidDataException) { return null; }
    }

    static VehicleGarage? Parse(SaveNode node)
    {
        var b = node.Data;
        var o = 0;
        o += sizeof(uint) + sizeof(uint) * (int)ReadU32(b, o);       // persistent id set
        o += sizeof(uint) + (int)ReadU32(b, o);           // skipSize + skipped bytes
        var count = ReadU32(b, o); o += sizeof(uint);
        for (uint i = 0; i < count; i++)
        {
            if (BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o)) == 0) { o += HashSize; continue; }
            var cls = BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o + EntryClassOffset));
            var size = (int)ReadU32(b, o + EntrySizeOffset);
            o += EntryHeaderSize;
            if (cls == GarageClass) return ParseEntry(node, o, size);
            o += size;
        }
        return null;
    }

    static VehicleGarage? ParseEntry(SaveNode node, int start, int size)
    {
        var b = node.Data;
        // shortcut: the array is found by its name+type hash, not by walking the entry's other properties; upgrade if a false hit ever shows up.
        var key = new byte[PropertyHeaderSize];
        BinaryPrimitives.WriteUInt64LittleEndian(key, ArrayName);
        BinaryPrimitives.WriteUInt64LittleEndian(key.AsSpan(HashSize), ArrayType);
        var at = b.AsSpan(start, size).IndexOf(key);
        if (at < 0) return null; // no vehicle unlocked yet: the property is left out, and adding it is not supported
        var arrayStart = start + at + PropertyHeaderSize;
        var count = ReadU32(b, arrayStart);
        var o = arrayStart + sizeof(uint);
        List<(VehicleId, byte[])> items = [];
        for (uint i = 0; i < count; i++)
        {
            var from = o;
            if (BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o)) != VehicleIdName || BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o + HashSize)) != VehicleIdType) return null;
            o += PropertyHeaderSize;
            VehicleId id = default;
            while (BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o)) is var name and not 0)
            {
                if (name != RecordIdName || BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o + HashSize)) != TweakDbId) return null;
                id = new VehicleId(ReadU32(b, o + PropertyHeaderSize), b[o + RecordLengthInProperty]);
                o += RecordPropertySize;
            }
            o += HashSize; // end of vehicleID
            if (BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o)) == HealthName) o += HealthSize;
            o += HashSize; // end of element
            items.Add((id, b.AsSpan(from, o - from).ToArray()));
        }
        // An array of structs ends at its last element's terminator; the array must sit inside the entry.
        return o <= start + size ? new VehicleGarage(node, start, arrayStart, o, items) : null;
    }

    static uint ReadU32(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));

    static ulong Fnv(string s) => Hashing.Fnv1a64(s);
}
