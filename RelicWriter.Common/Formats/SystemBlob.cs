using System.Buffers.Binary;
using System.Text;
using RelicWriter.Common.GameData;

namespace RelicWriter.Common.Formats;

/// <summary>
/// A "system blob" node payload (StatsSystem, ScriptableSystemsContainer, ...; see formats/systemBlob in docs/save-spec.xml): a string pool and a list of
/// objects whose fields name their own types. Parse then Write gives the same bytes; fields can be replaced or added through <see cref="BlobStruct"/>.
/// </summary>
public sealed class SystemBlob
{
    /// <summary>Header values kept as they are: the two u16 and the root names (empty unless the header root count is above 1).</summary>
    ushort unknown1, unknown2;

    // Blob header (all little-endian): size of the rest, two u16, root count, then four offsets; the root names (if any) and the string table follow.
    const int Unknown1Offset = 4, Unknown2Offset = 6, RootCountOffset = 8, StringDescOffsetOffset = 12, StringDataOffsetOffset = 16, ObjectDescOffsetOffset = 20, ObjectDataOffsetOffset = 24;
    const int HeaderSize = 28;
    /// <summary>The size field at the start counts everything after itself.</summary>
    const int SizeFieldSize = sizeof(uint);
    /// <summary>Root names are written only when there is more than one root.</summary>
    const uint SingleRoot = 1;
    // String descriptors: low 24 bits offset, high 8 bits length including the zero.
    const int StringDescSize = sizeof(uint), StringLengthShift = 24;
    const uint StringOffsetMask = 0xffffff, StringLengthMask = 0xff;
    // Object descriptors: class name index, then offset of the object's struct.
    const int ObjectDescSize = 2 * sizeof(uint), ObjectOffsetInDesc = sizeof(uint);
    /// <summary>Everything in the header after the size field: the two u16, root count and four offsets.</summary>
    const int HeaderAfterSize = HeaderSize - SizeFieldSize;
    uint rootCount;
    ulong[] rootNames = [];
    byte[] trailing = [];

    public List<string> Strings { get; } = [];

    public List<BlobObject> Objects { get; } = [];

    /// <summary>Parses the node data (the bytes after the node id).</summary>
    public static SystemBlob Parse(ReadOnlySpan<byte> d)
    {
        var blob = new SystemBlob();
        var blobSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(d);
        blob.unknown1 = BinaryPrimitives.ReadUInt16LittleEndian(d[Unknown1Offset..]);
        blob.unknown2 = BinaryPrimitives.ReadUInt16LittleEndian(d[Unknown2Offset..]);
        blob.rootCount = BinaryPrimitives.ReadUInt32LittleEndian(d[RootCountOffset..]);
        var strDesc = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[StringDescOffsetOffset..]);
        var strData = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[StringDataOffsetOffset..]);
        var objDescs = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[ObjectDescOffsetOffset..]);
        var objData = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[ObjectDataOffsetOffset..]);
        if (strDesc != 0) throw new InvalidDataException("Unexpected string descriptor offset.");
        var p = HeaderSize;
        if (blob.rootCount > SingleRoot)
        {
            var n = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[p..]);
            if (n != blob.rootCount) throw new InvalidDataException("Root name count mismatch.");
            p += sizeof(uint);
            blob.rootNames = new ulong[n];
            for (var i = 0; i < n; i++, p += sizeof(ulong)) blob.rootNames[i] = BinaryPrimitives.ReadUInt64LittleEndian(d[p..]);
        }
        var b = p; // base: all offsets are relative to it
        for (var i = 0; i < strData / StringDescSize; i++)
        {
            var desc = BinaryPrimitives.ReadUInt32LittleEndian(d[(b + StringDescSize * i)..]);
            var off = (int)(desc & StringOffsetMask);
            var len = (int)(desc >> StringLengthShift);
            // The length field is 8 bits, so a string of 256 or more characters stores its length modulo 256; the zero terminator gives the real end.
            var zero = d[(b + off)..].IndexOf((byte)0);
            if (zero < 0 || ((zero + 1) & StringLengthMask) != len) throw new InvalidDataException("String is not zero-terminated.");
            blob.Strings.Add(Encoding.Latin1.GetString(d.Slice(b + off, zero)));
        }
        var end = SizeFieldSize + blobSize;
        blob.trailing = d[end..].ToArray();
        var count = (objData - objDescs) / ObjectDescSize;
        for (var i = 0; i < count; i++)
        {
            var nameIndex = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[(b + objDescs + ObjectDescSize * i)..]);
            var start = b + (int)BinaryPrimitives.ReadUInt32LittleEndian(d[(b + objDescs + ObjectDescSize * i + ObjectOffsetInDesc)..]);
            var stop = i + 1 < count ? b + (int)BinaryPrimitives.ReadUInt32LittleEndian(d[(b + objDescs + ObjectDescSize * (i + 1) + ObjectOffsetInDesc)..]) : end;
            blob.Objects.Add(new BlobObject(nameIndex, BlobStruct.Parse(blob, d[start..stop], stop - start)));
        }
        return blob;
    }

    public byte[] Write()
    {
        var o = new MemoryStream();
        var pool = new MemoryStream();
        var descs = new uint[Strings.Count];
        var poolStart = StringDescSize * Strings.Count;
        for (var i = 0; i < Strings.Count; i++)
        {
            var bytes = Encoding.Latin1.GetBytes(Strings[i]);
            descs[i] = (uint)(poolStart + pool.Length) | (uint)(((bytes.Length + 1) & StringLengthMask) << StringLengthShift);
            pool.Write(bytes);
            pool.WriteByte(0);
        }
        var objDescs = poolStart + (int)pool.Length;
        var objData = objDescs + ObjectDescSize * Objects.Count;
        var objBytes = new MemoryStream();
        var objDescBytes = new MemoryStream();
        foreach (var obj in Objects)
        {
            U32(objDescBytes, (uint)obj.ClassIndex);
            U32(objDescBytes, (uint)(objData + objBytes.Length));
            objBytes.Write(obj.Body.ToBytes());
        }

        var rootBytes = rootCount > SingleRoot ? sizeof(uint) + sizeof(ulong) * rootNames.Length : 0;
        U32(o, (uint)(HeaderAfterSize + rootBytes + poolStart + pool.Length + objDescBytes.Length + objBytes.Length));
        o.Write(BitConverter.GetBytes(unknown1));
        o.Write(BitConverter.GetBytes(unknown2));
        U32(o, rootCount);
        U32(o, 0); // the string descriptors start at 0
        U32(o, (uint)poolStart);
        U32(o, (uint)objDescs);
        U32(o, (uint)objData);
        if (rootCount > SingleRoot)
        {
            U32(o, (uint)rootNames.Length);
            foreach (var r in rootNames) o.Write(BitConverter.GetBytes(r));
        }
        foreach (var d in descs) U32(o, d);
        pool.WriteTo(o);
        objDescBytes.WriteTo(o);
        objBytes.WriteTo(o);
        o.Write(trailing);
        return o.ToArray();
    }

    static void U32(Stream s, uint v) => s.Write(BitConverter.GetBytes(v));

    public int Intern(string s)
    {
        var i = Strings.IndexOf(s);
        if (i >= 0) return i;
        Strings.Add(s);
        return Strings.Count - 1;
    }

    public IEnumerable<BlobObject> OfClass(string className) => Objects.Where(o => Strings[o.ClassIndex] == className);
}

public sealed record BlobObject(int ClassIndex, BlobStruct Body);

public sealed class BlobField(int nameIndex, int typeIndex, byte[] data)
{
    public int NameIndex { get; } = nameIndex;
    public int TypeIndex { get; } = typeIndex;
    public byte[] Data { get; set; } = data;
}

/// <summary>An object or struct: a field count, field descriptors (name, type, offset) and the field data.</summary>
public sealed class BlobStruct(SystemBlob blob)
{
    // A struct: u16 field count, then per field a u16 name index, u16 type index and u32 offset.
    const int FieldCountSize = sizeof(ushort), FieldDescSize = 2 * sizeof(ushort) + sizeof(uint), FieldTypeInDesc = sizeof(ushort), FieldOffsetInDesc = 2 * sizeof(ushort);

    public List<BlobField> Fields { get; } = [];

    /// <summary>Parses a struct. When <paramref name="size"/> is not known (array elements) it is found from the last field's type.</summary>
    public static BlobStruct Parse(SystemBlob blob, ReadOnlySpan<byte> d, int? size, out int extent)
    {
        var s = new BlobStruct(blob);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(d);
        var offsets = new int[count];
        for (var i = 0; i < count; i++) offsets[i] = (int)BinaryPrimitives.ReadUInt32LittleEndian(d[(FieldCountSize + FieldDescSize * i + FieldOffsetInDesc)..]);
        extent = size ?? (count == 0 ? FieldCountSize : offsets[^1] + BlobTypes.Extent(blob, blob.Strings[BinaryPrimitives.ReadUInt16LittleEndian(d[(FieldCountSize + FieldDescSize * (count - 1) + FieldTypeInDesc)..])], d[offsets[^1]..]));
        for (var i = 0; i < count; i++)
        {
            var end = i + 1 < count ? offsets[i + 1] : extent;
            s.Fields.Add(new BlobField(BinaryPrimitives.ReadUInt16LittleEndian(d[(FieldCountSize + FieldDescSize * i)..]), BinaryPrimitives.ReadUInt16LittleEndian(d[(FieldCountSize + FieldDescSize * i + FieldTypeInDesc)..]), d[offsets[i]..end].ToArray()));
        }
        return s;
    }

    public static BlobStruct Parse(SystemBlob blob, ReadOnlySpan<byte> d, int size) => Parse(blob, d, size, out _);

    public byte[] ToBytes()
    {
        var o = new MemoryStream();
        o.Write(BitConverter.GetBytes((ushort)Fields.Count));
        var offset = FieldCountSize + FieldDescSize * Fields.Count;
        foreach (var f in Fields)
        {
            o.Write(BitConverter.GetBytes((ushort)f.NameIndex));
            o.Write(BitConverter.GetBytes((ushort)f.TypeIndex));
            o.Write(BitConverter.GetBytes((uint)offset));
            offset += f.Data.Length;
        }
        foreach (var f in Fields) o.Write(f.Data);
        return o.ToArray();
    }

    public BlobField? Find(string name) => Fields.FirstOrDefault(f => blob.Strings[f.NameIndex] == name);

    public BlobStruct? GetStruct(string name) => Find(name) is { } f ? Parse(blob, f.Data, f.Data.Length) : null;

    public ulong? GetUInt64(string name) => Find(name) is { Data.Length: sizeof(ulong) } f ? BinaryPrimitives.ReadUInt64LittleEndian(f.Data) : null;

    public int? GetInt(string name) => Find(name) is { Data.Length: sizeof(int) } f ? BinaryPrimitives.ReadInt32LittleEndian(f.Data) : null;

    /// <summary>The enum value or CName stored in a field (a pool index), or null when the field is not written (its default).</summary>
    public string? GetEnum(string name) => Find(name) is { Data.Length: sizeof(ushort) } f ? blob.Strings[BinaryPrimitives.ReadUInt16LittleEndian(f.Data)] : null;

    /// <summary>A new, empty struct that belongs to the same blob (so its names go into the blob's string pool).</summary>
    public BlobStruct NewStruct() => new(blob);

    public void SetInt(string name, int value) => Set(name, "Int32", BitConverter.GetBytes(value));

    public void SetEnum(string name, string enumType, string valueName) => Set(name, enumType, BitConverter.GetBytes((ushort)blob.Intern(valueName)));

    /// <summary>Replaces a field's bytes, or appends the field when the struct does not write it yet (a value equal to the default is left out by the game).</summary>
    void Set(string name, string typeName, byte[] data)
    {
        if (Find(name) is { } f) f.Data = data;
        else Fields.Add(new BlobField(blob.Intern(name), blob.Intern(typeName), data));
    }

    /// <summary>The elements of an "array:Struct" field.</summary>
    public List<BlobStruct> GetStructs(string name)
    {
        var list = new List<BlobStruct>();
        if (Find(name) is not { } f) return list;
        var d = f.Data.AsSpan();
        var n = (int)BinaryPrimitives.ReadUInt32LittleEndian(d);
        var p = sizeof(uint);
        for (var i = 0; i < n; i++)
        {
            list.Add(Parse(blob, d[p..], null, out var extent));
            p += extent;
        }
        if (p != d.Length) throw new InvalidDataException($"Array {name}: {d.Length - p} bytes left after the last element.");
        return list;
    }

    /// <summary>Writes the array back. A field the struct does not write yet (an empty array is left out by the game) is added with <paramref name="typeName"/>.</summary>
    public void SetStructs(string name, List<BlobStruct> items, string? typeName = null)
    {
        var o = new MemoryStream();
        o.Write(BitConverter.GetBytes((uint)items.Count));
        foreach (var item in items) o.Write(item.ToBytes());
        if (Find(name) is { } f) f.Data = o.ToArray();
        else if (typeName is not null) Fields.Add(new BlobField(blob.Intern(name), blob.Intern(typeName), o.ToArray()));
        else throw new InvalidOperationException($"Field {name} does not exist.");
    }
}

/// <summary>Sizes of values by type name, needed to find where an array element ends (element sizes are not stored).</summary>
public static class BlobTypes
{
    public static bool IsEnum(string type) => GameDatabase.Current.Enums.ContainsKey(type);

    /// <summary>The value an enum field has when the game leaves it out: the enum's value 0.</summary>
    public static string? EnumDefault(string type) => GameDatabase.Current.EnumDefault(type);

    public static int Extent(SystemBlob blob, string type, ReadOnlySpan<byte> d)
    {
        switch (type)
        {
            case "Bool" or "Uint8" or "Int8": return 1;
            case "Uint16" or "Int16" or "CName": return sizeof(ushort);
            case "Uint32" or "Int32" or "Float": return sizeof(uint);
            case "Uint64" or "Int64" or "TweakDBID" or "CRUID": return sizeof(ulong);
            case "NodeRef": return sizeof(ushort) + BinaryPrimitives.ReadUInt16LittleEndian(d);
            case "DataBuffer": return sizeof(uint) + (int)BinaryPrimitives.ReadUInt32LittleEndian(d);
        }
        if (type.StartsWith("handle:")) return sizeof(uint); // an index into the blob's objects
        string? element = null;
        if (type.StartsWith("array:")) element = type[6..];
        else if (type.StartsWith("static:")) element = type[(type.IndexOf(',') + 1)..];
        else if (type.StartsWith('[')) element = type[(type.IndexOf(']') + 1)..];
        if (element is not null)
        {
            var n = (int)BinaryPrimitives.ReadUInt32LittleEndian(d);
            var p = sizeof(uint);
            for (var i = 0; i < n; i++) p += Extent(blob, element, d[p..]);
            return p;
        }
        if (IsEnum(type)) return sizeof(ushort);
        BlobStruct.Parse(blob, d, null, out var extent);
        return extent;
    }
}
