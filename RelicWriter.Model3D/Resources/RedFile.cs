using System.Buffers.Binary;
using System.Text;
using RelicWriter.Common.Formats;
using RelicWriter.GameAssets;

namespace RelicWriter.Model3D.Resources;

/// <summary>A reference to another object in the same file (a handle). Numbered from 1; 0 is null.</summary>
public readonly record struct RedHandle(int Index);

/// <summary>A reference to another file, by its depot path (null for an empty reference).</summary>
public readonly record struct RedRef(string? Path);

/// <summary>A data buffer whose bytes live outside the object; <see cref="Raw"/> is the small value stored in place.</summary>
public sealed record RedBuffer(byte[] Raw)
{
    /// <summary>A deferred buffer is stored in a later segment of the archive entry: the value is its number (from 1) with the top bit set. Null for an inline value.</summary>
    public int? Segment => Raw.Length == sizeof(uint) && BitConverter.ToUInt32(Raw) is var v && (v & DeferredFlag) != 0 ? (int)(v & ~DeferredFlag) : null;

    const uint DeferredFlag = 0x80000000;
}

/// <summary>An object or struct: its class (the property type for a nested struct) and its properties, which are left out of the file when they hold their default.</summary>
public sealed class RedStruct(string className)
{
    public string ClassName { get; } = className;

    public Dictionary<string, object?> Properties { get; } = [];

    public object? this[string name] => Properties.GetValueOrDefault(name);

    public T? Get<T>(string name) => this[name] is T value ? value : default;

    public IReadOnlyList<object?> List(string name) => Get<List<object?>>(name) ?? [];

    public string? Text(string name) => this[name] as string;

    public long Int(string name, long fallback = 0) => this[name] is long l ? l : fallback;

    public double Number(string name, double fallback = 0) => this[name] is double d ? d : fallback;
}

/// <summary>
/// A game resource file (CR2W) read into plain values: every object with all its properties. This is the common layer under the appearance,
/// mesh and material readers. Values: bool, long and ulong for integers, double for floats, string for names and strings,
/// <see cref="RedHandle"/>, <see cref="RedRef"/>, <see cref="RedBuffer"/>, List of values for arrays, <see cref="RedStruct"/>.
/// </summary>
/// <remarks>Built from files of the 2.5 game; property types are told apart by name, and any type that isn't a known value kind is read as a struct.</remarks>
public sealed class RedFile
{
    readonly Cr2w cr2w;

    RedFile(Cr2w cr2w)
    {
        this.cr2w = cr2w;
        Objects = Enumerable.Range(0, cr2w.ObjectCount).Select(ReadObject).ToList();
    }

    public List<RedStruct> Objects { get; }

    /// <summary>The paths of the files this one refers to.</summary>
    public IReadOnlyList<string> Imports => cr2w.Imports;

    /// <summary>The bytes an object occupies in the file, for data that follows its properties.</summary>
    public ReadOnlySpan<byte> ObjectBytes(int number) => cr2w.ObjectBytes(number);

    public static RedFile? Parse(byte[] data) => Cr2w.Parse(data) is { } c ? new RedFile(c) : null;

    /// <summary>The object a handle points to, or null.</summary>
    public RedStruct? Resolve(object? handle) => handle is RedHandle { Index: > 0 } h && h.Index <= Objects.Count ? Objects[h.Index - 1] : null;

    // ---- values ----

    RedStruct ReadObject(int number)
    {
        var result = ReadStruct(cr2w.ObjectClass(number), cr2w.Object(number, out var end));
        if (result.ClassName == MaterialInstance) result.Properties[MaterialValues] = ReadMaterialValues(end);
        return result;
    }

    /// <summary>A material instance keeps its parameters after the properties: a count, then for each a size (counting itself), a name, a type and the value.</summary>
    List<object?> ReadMaterialValues(int at)
    {
        var values = new List<object?>();
        var count = (int)cr2w.U32At(at);
        var p = at + sizeof(uint);
        for (var i = 0; i < count; i++)
        {
            var size = (int)cr2w.U32At(p);
            var name = cr2w.Name(cr2w.U16(p + sizeof(uint)));
            var type = cr2w.Name(cr2w.U16(p + sizeof(uint) + sizeof(ushort)));
            var value = p + sizeof(uint) + 2 * sizeof(ushort);
            var entry = new RedStruct(type);
            try { entry.Properties[name] = ReadValue(type, value, size - value + p, out _); }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException) { entry.Properties[name] = new RedBuffer(cr2w.Data.AsSpan(value, size - value + p).ToArray()); }
            values.Add(entry);
            p += size;
        }
        return values;
    }

    /// <summary>The class whose parameters follow its properties, and the name they are stored under here.</summary>
    const string MaterialInstance = "CMaterialInstance", MaterialValues = "values";

    RedStruct ReadStruct(string className, List<Cr2w.Prop> props)
    {
        var result = new RedStruct(className);
        foreach (var p in props)
        {
            // A type this reader doesn't know (flag sets, for example) can't be told from a struct until it fails; keep its bytes.
            try { result.Properties[p.Name] = ReadValue(p.Type, p.Value, p.Length, out _); }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException) { result.Properties[p.Name] = new RedBuffer(cr2w.Data.AsSpan(p.Value, p.Length).ToArray()); }
        }
        return result;
    }

    /// <summary>Reads a value of the given type at <paramref name="at"/>; <paramref name="end"/> is the position after it (for array elements, which carry no length of their own).</summary>
    object? ReadValue(string type, int at, int length, out int end)
    {
        var d = cr2w.Data;
        end = at + length;
        switch (type)
        {
            case "Bool": end = at + 1; return d[at] != 0;
            case "Int8": end = at + 1; return (long)(sbyte)d[at];
            case "Uint8": end = at + 1; return (long)d[at];
            case "Int16": end = at + 2; return (long)BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(at));
            case "Uint16": end = at + 2; return (long)cr2w.U16(at);
            case "Int32": end = at + 4; return (long)BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(at));
            case "Uint32": end = at + 4; return (long)cr2w.U32At(at);
            case "Int64": end = at + 8; return BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(at));
            case "Uint64" or "TweakDBID" or "CRUID": end = at + 8; return BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(at));
            case "Float": end = at + 4; return (double)cr2w.Float(at);
            case "Double": end = at + 8; return BinaryPrimitives.ReadDoubleLittleEndian(d.AsSpan(at));
            case "CName": end = at + 2; return cr2w.Name(cr2w.U16(at));
            case "String":
            {
                var p = at;
                var n = OnScreens.ReadLength(d, ref p);
                end = p + n;
                return Encoding.UTF8.GetString(d, p, n);
            }
            case "DataBuffer" or "SharedDataBuffer" or "serializationDeferredDataBuffer": return new RedBuffer(d.AsSpan(at, length).ToArray());
        }
        if (BlobTypes.IsEnum(type)) { end = at + 2; return cr2w.Name(cr2w.U16(at)); }
        if (type.StartsWith("handle:") || type.StartsWith("whandle:")) { end = at + 4; return new RedHandle(BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(at))); }
        if (type.StartsWith("rRef:") || type.StartsWith("raRef:"))
        {
            end = at + 2;
            var index = cr2w.U16(at);
            return new RedRef(index == 0 ? null : cr2w.Imports[index - 1]);
        }
        // "static:32,T" is a fixed-size array; it is stored like a dynamic one (a count, then the elements).
        if (type.StartsWith("array:") || type.StartsWith("static:"))
        {
            var element = type.StartsWith("array:") ? type["array:".Length..] : type[(type.IndexOf(',') + 1)..];
            var count = (int)cr2w.U32At(at);
            var list = new List<object?>(count);
            var p = at + 4;
            for (var i = 0; i < count; i++) list.Add(ReadValue(element, p, 0, out p));
            end = p;
            return list;
        }
        // Anything else is a struct: a flag byte, then properties until a name index of 0.
        return ReadStruct(type, cr2w.Struct(at, out end));
    }
}
