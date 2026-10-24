using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using RelicWriter.Model3D.Resources;

namespace RelicWriter.Model3D.Textures;

/// <summary>A decoded image: RGBA, four bytes per pixel. Rows are in the order the file stores them: the first row is v = 0 (the bottom of the picture), so it can be handed to OpenGL as is and sampled with the mesh's v unchanged.</summary>
public sealed record Texture(int Width, int Height, byte[] Rgba);

/// <summary>
/// Reads a .xbm texture: the largest mip stored in the file, decoded to RGBA. The file's top mips (it can be 4096 wide) are streamed from elsewhere;
/// what is stored in the archive entry is already a usable size (1024 to 2048 for the character textures).
/// </summary>
/// <remarks>Block decoding is by BCnEncoder.Net (MIT). Formats seen on the character's skin, head and hair textures are covered; any other compression throws <see cref="NotSupportedException"/> naming it.</remarks>
public static class TextureReader
{
    /// <summary>The deferred buffer holding the pixel data is the first segment after the header.</summary>
    const int PixelSegment = 1;

    /// <summary>The compression setting of the file, mapped to a block format.</summary>
    static CompressionFormat FormatOf(string compression) => compression switch
    {
        "TCM_DXTNoAlpha" => CompressionFormat.Bc1,
        "TCM_DXTAlpha" => CompressionFormat.Bc3,
        "TCM_QualityR" => CompressionFormat.Bc4,
        "TCM_Normalmap" or "TCM_QualityRG" => CompressionFormat.Bc5,
        "TCM_QualityColor" => CompressionFormat.Bc7,
        _ => throw new NotSupportedException("Texture compression " + compression),
    };

    public static Texture? Read(GameResources res, string path)
    {
        if (res.File(path) is not { } file) return null;
        var texture = file.Objects[0];
        var blob = file.Objects.FirstOrDefault(o => o.ClassName.StartsWith("rendRenderTextureBlob", StringComparison.Ordinal));
        var header = blob?.Get<RedStruct>("header");
        if (header is null || res.Bytes(path, PixelSegment) is not { } data) return null;

        var size = header.Get<RedStruct>("sizeInfo");
        int width = (int)(size?.Int("width") ?? 0), height = (int)(size?.Int("height") ?? 0);
        var format = FormatOf(texture.Get<RedStruct>("setup")?.Text("compression") ?? "");

        // The first mip is at offset 0 and as large as the image.
        var first = header.List("mipMapInfo").OfType<RedStruct>().FirstOrDefault()?.Get<RedStruct>("placement");
        var length = (int)(first?.Int("size") ?? data.Length);
        var pixels = new BcDecoder().DecodeRaw(data.AsSpan(0, length).ToArray(), width, height, format);

        var rgba = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            rgba[i * 4] = pixels[i].r;
            rgba[i * 4 + 1] = pixels[i].g;
            rgba[i * 4 + 2] = pixels[i].b;
            rgba[i * 4 + 3] = pixels[i].a;
        }
        return new Texture(width, height, rgba);
    }
}
