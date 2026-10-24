using RelicWriter.Common;
using System.Buffers.Binary;
using System.Text;

namespace RelicWriter.GameAssets;

/// <summary>
/// A minimal reader for the game's CR2W resource files: the name table, the import (referenced file) table, and the property lists of the
/// objects. It reads only what texture atlases and textures need.
/// </summary>
/// <remarks>
/// Header (version 195): "CR2W", then 0x28: table descriptors of (u32 offset, u32 count, u32 crc): 0 string blob, 1 names (u32 string offset,
/// u32 hash), 2 imports (u32 string offset, u16 class name, u16 flags), 3 properties, 4 objects (24 bytes: u16 class name, u16 flags, u32 parent,
/// u32 size, u32 offset, u32 template, u32 crc). A property is u16 name index, u16 type index, u32 size (counting itself) and the value; a
/// struct value (and an object, and each element of an array of structs) is one flag byte, then properties until a name index of 0.
/// Checked on inkTextureAtlas and CBitmapTexture files of the 2.5 game only.
/// </remarks>
internal sealed class Cr2w
{
    public const uint Magic = 0x57325243; // "CR2W"
    // Header: where the string blob, name table, import table and object table are (offset, then count at +4 for the last three).
    public const int StringsOffsetField = 0x28, NamesOffsetField = 0x34, NameCountField = 0x38, ImportsOffsetField = 0x40, ImportCountField = 0x44,
        ObjectsOffsetField = 0x58, ObjectCountField = 0x5C, HeaderSize = 0x64;
    // Table rows: a name or import is a u32 string offset (+ 4 more bytes); an object is 24 bytes with its data offset at 12.
    const int NameRowSize = 8, ImportRowSize = 8, ObjectRowSize = 24, ObjectSizeOffset = 8, ObjectDataOffset = 12;
    // A property: u16 name index, u16 type index (at 2), u32 size (at 4, counting itself), then the value (at 8). A struct starts with a flag byte and ends with a zero name index.
    public const int PropertyTypeOffset = sizeof(ushort), PropertySizeOffset = 2 * sizeof(ushort), PropertyValueOffset = 8, SizeFieldSize = sizeof(uint);
    const int StructFlagSize = 1, StructEndSize = sizeof(ushort);

    public sealed record Prop(string Name, string Type, int Value, int Length);

    readonly byte[] data;
    readonly string[] names;
    public string[] Imports { get; }
    readonly int[] objects;
    readonly string[] objectClasses;
    readonly int[] objectSizes;

    Cr2w(byte[] data)
    {
        this.data = data;
        var strings = (int)U32(StringsOffsetField);
        string Text(int offset) => Encoding.UTF8.GetString(data, strings + offset, Array.IndexOf(data, (byte)0, strings + offset) - strings - offset);

        names = Enumerable.Range(0, (int)U32(NameCountField)).Select(i => Text((int)U32((int)U32(NamesOffsetField) + i * NameRowSize))).ToArray();
        Imports = Enumerable.Range(0, (int)U32(ImportCountField)).Select(i => Text((int)U32((int)U32(ImportsOffsetField) + i * ImportRowSize))).ToArray();
        objectClasses = Enumerable.Range(0, (int)U32(ObjectCountField)).Select(i => names[U16((int)U32(ObjectsOffsetField) + i * ObjectRowSize)]).ToArray();
        objectSizes = Enumerable.Range(0, (int)U32(ObjectCountField)).Select(i => (int)U32((int)U32(ObjectsOffsetField) + i * ObjectRowSize + ObjectSizeOffset)).ToArray();
        objects = Enumerable.Range(0, (int)U32(ObjectCountField)).Select(i => (int)U32((int)U32(ObjectsOffsetField) + i * ObjectRowSize + ObjectDataOffset)).ToArray();
    }

    public static Cr2w? Parse(byte[] data) => data.Length > HeaderSize && BinaryPrimitives.ReadUInt32LittleEndian(data) == Magic ? new Cr2w(data) : null;

    uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));

    public ushort U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at));

    public uint U32At(int at) => U32(at);

    public float Float(int at) => BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(at));

    public string Name(int index) => names[index];

    public int ObjectCount => objects.Length;

    /// <summary>The class of an object (an export), for example appearanceAppearanceResource.</summary>
    public string ObjectClass(int number) => objectClasses[number];

    /// <summary>The bytes an object occupies in the file (properties and anything after them).</summary>
    public ReadOnlySpan<byte> ObjectBytes(int number) => data.AsSpan(objects[number], objectSizes[number]);

    public int Length => data.Length;

    public byte[] Data => data;

    /// <summary>The properties of the object with this number (an offset to its data is in the object table).</summary>
    public List<Prop> Object(int number) => Struct(objects[number], out _);

    /// <summary>The properties of an object; <paramref name="end"/> is the position after them, where data of the class's own can follow.</summary>
    public List<Prop> Object(int number, out int end) => Struct(objects[number], out end);

    /// <summary>The properties of a struct that starts at <paramref name="at"/>; <paramref name="end"/> is the position after it.</summary>
    public List<Prop> Struct(int at, out int end)
    {
        var props = new List<Prop>();
        var p = at + StructFlagSize;
        while (U16(p) != 0)
        {
            var size = (int)U32(p + PropertySizeOffset);
            props.Add(new Prop(names[U16(p)], names[U16(p + PropertyTypeOffset)], p + PropertyValueOffset, size - SizeFieldSize));
            p += SizeFieldSize + size;
        }
        end = p + StructEndSize;
        return props;
    }

    /// <summary>The elements of an array of structs whose value starts at <paramref name="at"/> (its u32 count).</summary>
    public IEnumerable<List<Prop>> Elements(int at)
    {
        var count = (int)U32(at);
        var p = at + sizeof(uint);
        for (var i = 0; i < count; i++)
        {
            yield return Struct(p, out var end);
            p = end;
        }
    }
}

/// <summary>An inkTextureAtlas: named rectangles (in 0..1 texture coordinates) of a texture.</summary>
internal sealed class InkAtlas
{
    public required string Texture { get; init; }
    public required Dictionary<string, (float Left, float Top, float Right, float Bottom)> Parts { get; init; }

    /// <summary>The first slot, which is the full-size texture (the later slots repeat the parts for smaller textures).</summary>
    public static InkAtlas? Parse(byte[] file)
    {
        if (Cr2w.Parse(file) is not { } cr2w) return null;
        var slots = cr2w.Object(0).FirstOrDefault(p => p.Name == "slots");
        if (slots is null) return null;
        var slot = cr2w.Elements(slots.Value).FirstOrDefault();
        if (slot?.FirstOrDefault(p => p.Name == "texture") is not { } texture || slot.FirstOrDefault(p => p.Name == "parts") is not { } parts) return null;

        var rects = new Dictionary<string, (float, float, float, float)>();
        foreach (var part in cr2w.Elements(parts.Value))
        {
            var name = part.FirstOrDefault(p => p.Name == "partName");
            var rect = part.FirstOrDefault(p => p.Name == "clippingRectInUVCoords");
            if (name is null || rect is null) continue;
            // Zero sides are left out of the file.
            float Side(string side) => cr2w.Struct(rect.Value, out _).FirstOrDefault(p => p.Name == side) is { } s ? cr2w.Float(s.Value) : 0;
            rects[cr2w.Name(cr2w.U16(name.Value))] = (Side("Left"), Side("Top"), Side("Right"), Side("Bottom"));
        }
        // The texture is an import, numbered from 1.
        return new InkAtlas { Texture = cr2w.Imports[cr2w.U16(texture.Value) - 1], Parts = rects };
    }
}

/// <summary>A block-compressed texture format the game's icon textures use.</summary>
internal static class Bc3
{
    // A block is 4x4 pixels in 16 bytes: 8 bytes of alpha (two end values, then 16 3-bit indices from bit 16), then 8 of colour (two RGB565 values at 8 and 10, then 16 2-bit indices at 12).
    const int BlockSide = 4, BlockBytes = 16, PixelsPerBlock = BlockSide * BlockSide, Channels = 4;
    const int AlphaIndicesShift = 16, AlphaIndexBits = 3, AlphaIndexMask = 7, ColorIndexBits = 2, ColorIndexMask = 3;
    const int Color0Offset = 8, Color1Offset = 10, ColorIndicesOffset = 12;
    const int AlphaPaletteSize = 8, ColorPaletteSize = 4, OpaqueAlpha = 255;
    // The alpha palette interpolates 6 values between the ends, or 4 plus 0 and 255 when the first end is not larger.
    const int InterpolatedWide = 6, InterpolatedNarrow = 4, StepsWide = 7, StepsNarrow = 5;
    // RGB565 and the 8-bit channel layout.
    const int RedShift = 11, GreenShift = 5, FiveBitMax = 31, SixBitMax = 63, GreenMask = 63, FiveBitMask = 31, ByteMax = 255;
    const int RedPack = 16, GreenPack = 8, ByteBits = 8, AlphaOffset = 3, GreenOffset = 1, BlueOffset = 2;

    /// <summary>Decodes a rectangle of a BC3 (DXT5) texture, stored as rows of 4x4 blocks of 16 bytes, to RGBA bytes.</summary>
    public static byte[] DecodeRegion(byte[] data, int textureWidth, int x, int y, int width, int height)
    {
        var rgba = new byte[width * height * Channels];
        var blocksPerRow = textureWidth / BlockSide;
        Span<byte> alpha = stackalloc byte[AlphaPaletteSize];
        Span<int> colors = stackalloc int[ColorPaletteSize];
        for (var by = y / BlockSide; by <= (y + height - 1) / BlockSide; by++)
            for (var bx = x / BlockSide; bx <= (x + width - 1) / BlockSide; bx++)
            {
                var block = data.AsSpan((by * blocksPerRow + bx) * BlockBytes, BlockBytes);
                Palette(block, alpha, colors);
                var alphaBits = BinaryPrimitives.ReadUInt64LittleEndian(block) >> AlphaIndicesShift;
                var colorBits = BinaryPrimitives.ReadUInt32LittleEndian(block[ColorIndicesOffset..]);
                for (var i = 0; i < PixelsPerBlock; i++)
                {
                    int px = bx * BlockSide + i % BlockSide - x, py = by * BlockSide + i / BlockSide - y;
                    if (px < 0 || py < 0 || px >= width || py >= height) continue;
                    var c = colors[(int)(colorBits >> (ColorIndexBits * i)) & ColorIndexMask];
                    var at = (py * width + px) * Channels;
                    rgba[at] = (byte)(c >> RedPack);
                    rgba[at + GreenOffset] = (byte)(c >> GreenPack);
                    rgba[at + BlueOffset] = (byte)c;
                    rgba[at + AlphaOffset] = alpha[(int)(alphaBits >> (AlphaIndexBits * i)) & AlphaIndexMask];
                }
            }
        return rgba;
    }

    static void Palette(ReadOnlySpan<byte> block, Span<byte> alpha, Span<int> colors)
    {
        int a0 = block[0], a1 = block[1];
        alpha[0] = (byte)a0;
        alpha[1] = (byte)a1;
        for (var i = 1; i <= (a0 > a1 ? InterpolatedWide : InterpolatedNarrow); i++)
            alpha[i + 1] = (byte)(a0 > a1 ? ((StepsWide - i) * a0 + i * a1) / StepsWide : ((StepsNarrow - i) * a0 + i * a1) / StepsNarrow);
        if (a0 <= a1) { alpha[AlphaPaletteSize - 2] = 0; alpha[AlphaPaletteSize - 1] = OpaqueAlpha; }

        int c0 = BinaryPrimitives.ReadUInt16LittleEndian(block[Color0Offset..]), c1 = BinaryPrimitives.ReadUInt16LittleEndian(block[Color1Offset..]);
        var (r0, g0, b0) = Rgb565(c0);
        var (r1, g1, b1) = Rgb565(c1);
        colors[0] = Pack(r0, g0, b0);
        colors[1] = Pack(r1, g1, b1);
        // BC3 colour blocks always use the four-colour form.
        colors[2] = Pack((2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3);
        colors[3] = Pack((r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3);
    }

    static (int, int, int) Rgb565(int c) => ((c >> RedShift & FiveBitMask) * ByteMax / FiveBitMax, (c >> GreenShift & GreenMask) * ByteMax / SixBitMax, (c & FiveBitMask) * ByteMax / FiveBitMax);

    static int Pack(int r, int g, int b) => r << RedPack | g << GreenPack | b;
}
