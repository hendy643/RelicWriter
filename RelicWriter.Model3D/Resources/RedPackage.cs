using System.Text;
using RelicWriter.Common.Formats;

namespace RelicWriter.Model3D.Resources;

/// <summary>
/// The compiled-data buffer of an entity (.ent) or appearance (.app) file: the component objects as one block with its own
/// path pool, name pool and object table. Objects are read as <see cref="RedStruct"/>s; a handle is the index of another object here.
/// </summary>
/// <remarks>Layout worked out from base-game entity files: header words 0x0C..0x1C are the offsets (from <see cref="Base"/>) of the name
/// descriptors, name pool, object table and object data. Values of types not listed in <c>Value</c> are kept as <see cref="RedBuffer"/>.</remarks>
public sealed class RedPackage
{
    /// <summary>Where the offsets in the header start from.</summary>
    const int Base = 0x4C;

    const int PathsEndField = 0x0C, NamesField = 0x10, NamePoolField = 0x14, ObjectsField = 0x18, DataField = 0x1C;
    const int DescriptorSize = 4, ObjectEntrySize = 8, FieldSize = 8, OffsetMask = 0xFFFFFF, LengthShift = 24, HalfLengthFlag = 0x800000;

    readonly byte[] d;
    readonly List<string> names = [];

    public List<string> Paths { get; } = [];

    public List<RedStruct> Objects { get; } = [];

    public static RedPackage? Parse(byte[]? data)
    {
        if (data is null || data.Length < Base) return null;
        try { return new RedPackage(data); }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException) { return null; }
    }

    RedPackage(byte[] data)
    {
        d = data;
        int pathsEnd = (int)U32(PathsEndField), namesAt = (int)U32(NamesField), poolAt = (int)U32(NamePoolField), tableAt = (int)U32(ObjectsField), dataAt = (int)U32(DataField);
        for (var p = Base; p < Base + pathsEnd; p += DescriptorSize)
        {
            var v = U32(p);
            var length = (int)(v >> LengthShift) * 2 + ((v & HalfLengthFlag) != 0 ? 1 : 0);
            Paths.Add(Encoding.ASCII.GetString(d, Base + (int)(v & (OffsetMask & ~HalfLengthFlag)), length));
        }
        for (var p = Base + namesAt; p < Base + poolAt; p += DescriptorSize)
        {
            var v = U32(p);
            names.Add(Encoding.ASCII.GetString(d, Base + (int)(v & OffsetMask), (int)(v >> LengthShift) - 1));
        }
        var table = new List<(string cls, int at)>();
        for (var p = Base + tableAt; p < Base + dataAt; p += ObjectEntrySize) table.Add((names[(int)U32(p)], Base + (int)U32(p + 4)));
        for (var i = 0; i < table.Count; i++) Objects.Add(ReadStruct(table[i].cls, table[i].at, i + 1 < table.Count ? table[i + 1].at : d.Length));
    }

    uint U32(int at) => BitConverter.ToUInt32(d, at);

    ushort U16(int at) => BitConverter.ToUInt16(d, at);

    RedStruct ReadStruct(string cls, int start, int end)
    {
        var result = new RedStruct(cls);
        int count = U16(start);
        for (var k = 0; k < count; k++)
        {
            var at = start + 2 + k * FieldSize;
            var from = start + (int)U32(at + 4);
            var to = k + 1 < count ? start + (int)U32(at + 4 + FieldSize) : end;
            result.Properties[names[U16(at)]] = Value(names[U16(at + 2)], from, to);
        }
        return result;
    }

    object? Value(string type, int from, int to)
    {
        if (BlobTypes.IsEnum(type)) return names[U16(from)];
        return type switch
        {
            "CName" => names[U16(from)],
            "Float" => (double)BitConverter.ToSingle(d, from),
            "Uint8" => (long)d[from],
            "Bool" => d[from] != 0,
            "Int32" => (long)BitConverter.ToInt32(d, from),
            "Uint32" => (long)U32(from),
            _ when type.StartsWith("handle:", StringComparison.Ordinal) => new RedHandle(BitConverter.ToInt32(d, from)),
            _ when type.StartsWith("raRef:", StringComparison.Ordinal) || type.StartsWith("rRef:", StringComparison.Ordinal) => new RedRef(Paths[U16(from)]),
            _ => new RedBuffer(d[from..to]),
        };
    }
}
