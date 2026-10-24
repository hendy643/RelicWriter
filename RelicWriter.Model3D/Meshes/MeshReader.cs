using System.Buffers.Binary;
using System.Numerics;
using RelicWriter.Model3D.Resources;

namespace RelicWriter.Model3D.Meshes;

/// <summary>One drawable piece of a mesh: it has one material (named in the mesh's appearances).</summary>
public sealed class MeshChunk
{
    /// <summary>Position in metres, three floats per vertex.</summary>
    public required float[] Positions { get; init; }

    /// <summary>Unit normals (pointing outward; the triangles wind clockwise seen from outside, in the game's axes), three floats per vertex; empty when the mesh has none.</summary>
    public required float[] Normals { get; init; }

    /// <summary>First texture coordinates, two floats per vertex (v measured from the top, as in the texture file).</summary>
    public required float[] Uvs { get; init; }

    /// <summary>Triangle list.</summary>
    public required int[] Indices { get; init; }

    public int VertexCount => Positions.Length / 3;
}

public sealed class MeshData
{
    public required List<MeshChunk> Chunks { get; init; }

    /// <summary>Material appearances of the mesh: appearance name, then the material name each chunk uses.</summary>
    public required Dictionary<string, List<string>> Appearances { get; init; }
}

/// <summary>
/// Reads a .mesh file: the chunks' vertices and triangles from the render blob. The vertex layout is read from the file; the packings seen in
/// player and hair meshes are supported, and any other throws <see cref="NotSupportedException"/> naming it.
/// </summary>
/// <remarks>Checked on the female player head and body meshes of the 2.5 game. Skin weights and indices, vertex colours and the second texture coordinates are skipped.</remarks>
public static class MeshReader
{
    const float ShortNormalizer = short.MaxValue, Dec4Half = 511.5f;
    const int Dec4Bits = 10, Dec4Mask = 0x3FF;
    const int IndexSize = sizeof(ushort);

    /// <summary>The chunk data of a mesh. <paramref name="renderBuffer"/> is the blob's buffer (vertices, then indices).</summary>
    public static MeshData Read(RedFile mesh, Func<RedBuffer, byte[]?> loadBuffer)
    {
        var root = mesh.Objects[0];
        var blob = mesh.Resolve(root["renderResourceBlob"]) ?? throw new InvalidDataException("The mesh has no render blob.");
        var header = blob.Get<RedStruct>("header") ?? throw new InvalidDataException("The render blob has no header.");
        var data = blob.Get<RedBuffer>("renderBuffer") is { } b ? loadBuffer(b) : null;
        if (data is null) throw new InvalidDataException("The mesh's vertex data could not be read.");

        var scale = Vector(header.Get<RedStruct>("quantizationScale"));
        var offset = Vector(header.Get<RedStruct>("quantizationOffset"));
        var indexBase = (int)header.Int("indexBufferOffset");

        var chunks = new List<MeshChunk>();
        foreach (var info in header.List("renderChunkInfos").OfType<RedStruct>())
        {
            var vertices = info.Get<RedStruct>("chunkVertices")!;
            var layout = vertices.Get<RedStruct>("vertexLayout")!;
            var offsets = vertices.List("byteOffsets").Select(o => (int)(long)o!).ToArray();
            var strides = layout.List("slotStrides").Select(o => (int)(long)o!).ToArray();
            var count = (int)info.Int("numVertices");
            var indexCount = (int)info.Int("numIndices");
            var indexStart = indexBase + (int)(info.Get<RedStruct>("chunkIndices")?.Int("teOffset") ?? 0);

            chunks.Add(ReadChunk(data, layout, offsets, strides, count, scale, offset, indexStart, indexCount));
        }

        var appearances = new Dictionary<string, List<string>>();
        foreach (var handle in root.List("appearances"))
            if (mesh.Resolve(handle) is { } a && a.Text("name") is { } name)
                appearances.TryAdd(name, a.List("chunkMaterials").OfType<string>().ToList());
        return new MeshData { Chunks = chunks, Appearances = appearances };
    }

    static MeshChunk ReadChunk(byte[] data, RedStruct layout, int[] offsets, int[] strides, int count, Vector4 scale, Vector4 offset, int indexStart, int indexCount)
    {
        var positions = new float[count * 3];
        var normals = new float[count * 3];
        var uvs = new float[count * 2];
        var inStream = new int[strides.Length]; // running offset of the next element inside each stream's vertex

        foreach (var element in layout.List("elements").OfType<RedStruct>())
        {
            if (element.Text("type") is not { } type || type == "PT_Invalid") continue; // an unused slot of the fixed-size list
            var usage = element.Text("usage");
            var index = (int)element.Int("usageIndex");
            var stream = (int)element.Int("streamIndex");
            if (stream >= offsets.Length) continue; // per-instance data (stream 7), not part of the vertex buffer
            var at = inStream[stream];
            inStream[stream] += SizeOf(type);

            // Only the first of each usage is decoded; the rest still advance the offset above.
            if (index != 0) continue;
            for (var v = 0; v < count; v++)
            {
                var p = offsets[stream] + v * strides[stream] + at;
                switch (usage)
                {
                    case "PS_Position":
                    {
                        var x = Unpack(data, type, p);
                        positions[v * 3] = x[0] * scale.X + offset.X;
                        positions[v * 3 + 1] = x[1] * scale.Y + offset.Y;
                        positions[v * 3 + 2] = x[2] * scale.Z + offset.Z;
                        break;
                    }
                    case "PS_Normal":
                    {
                        var x = Unpack(data, type, p);
                        normals[v * 3] = x[0]; normals[v * 3 + 1] = x[1]; normals[v * 3 + 2] = x[2];
                        break;
                    }
                    case "PS_TexCoord":
                    {
                        var x = Unpack(data, type, p);
                        uvs[v * 2] = x[0]; uvs[v * 2 + 1] = x[1];
                        break;
                    }
                }
            }
        }

        var indices = new int[indexCount];
        for (var i = 0; i < indexCount; i++) indices[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(indexStart + i * IndexSize));
        return new MeshChunk { Positions = positions, Normals = normals, Uvs = uvs, Indices = indices };
    }

    static int SizeOf(string type) => type switch
    {
        "PT_Float1" => 4,
        "PT_Float2" => 8,
        "PT_Float3" => 12,
        "PT_Float4" => 16,
        "PT_Float16_2" => 4,
        "PT_Float16_4" => 8,
        "PT_Short2N" or "PT_Short2" => 4,
        "PT_Short4N" or "PT_Short4" => 8,
        "PT_UShort4N" or "PT_UShort4" => 8,
        "PT_UByte4" or "PT_UByte4N" or "PT_Color" or "PT_Dec4" => 4,
        _ => throw new NotSupportedException("Vertex packing " + type),
    };

    /// <summary>The leading components of a packed value as floats (normalised types are scaled to -1..1 or 0..1).</summary>
    static float[] Unpack(byte[] d, string type, int p)
    {
        var s = d.AsSpan(p);
        switch (type)
        {
            case "PT_Float2": return [BitConverter.ToSingle(s), BitConverter.ToSingle(s[4..])];
            case "PT_Float3": return [BitConverter.ToSingle(s), BitConverter.ToSingle(s[4..]), BitConverter.ToSingle(s[8..])];
            case "PT_Float16_2": return [(float)BitConverter.ToHalf(s), (float)BitConverter.ToHalf(s[2..])];
            case "PT_Short4N": return [BinaryPrimitives.ReadInt16LittleEndian(s) / ShortNormalizer, BinaryPrimitives.ReadInt16LittleEndian(s[2..]) / ShortNormalizer, BinaryPrimitives.ReadInt16LittleEndian(s[4..]) / ShortNormalizer];
            case "PT_Dec4":
            {
                var v = BinaryPrimitives.ReadUInt32LittleEndian(s);
                // Unsigned 10-bit channels mapped to -1..1 (checked: 0.99 agreement with the triangle normals).
                return [Unsigned(v), Unsigned(v >> Dec4Bits), Unsigned(v >> 2 * Dec4Bits)];
            }
            default: throw new NotSupportedException("Reading vertex packing " + type);
        }
    }

    static float Unsigned(uint bits) => (bits & Dec4Mask) / Dec4Half - 1;

    static Vector4 Vector(RedStruct? v) => v is null ? Vector4.One : new((float)v.Number("X"), (float)v.Number("Y"), (float)v.Number("Z"), (float)v.Number("W"));
}
