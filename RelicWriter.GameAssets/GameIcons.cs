namespace RelicWriter.GameAssets;

/// <summary>A decoded picture: RGBA bytes, rows from the top.</summary>
public sealed record GameImage(int Width, int Height, byte[] Rgba);

/// <summary>
/// Pictures from the player's own game files, found by the atlas and part the game database names for them: the part is cut out of the atlas texture.
/// Windows only for now (the archives need the game's Oodle DLL, see <see cref="Oodle"/>).
/// </summary>
public sealed class GameIcons
{
    readonly GameFiles data;
    readonly Dictionary<ulong, Atlas?> atlases = [];

    sealed record Atlas(InkAtlas Parts, int Width, byte[] Pixels);

    GameIcons(GameFiles data) => this.data = data;

    /// <summary>The icons in use for the app; set once the game folder is known. Null until then, or when the game files can't be read.</summary>
    public static GameIcons? Current { get; set; }

    /// <summary>Opens the game's UI archives. Null if the folder isn't a readable install.</summary>
    public static GameIcons? Open(string gameDir)
    {
        try
        {
            if (!Oodle.TryLoad(gameDir)) return null;
            var archives = Path.Combine(gameDir, "archive", "pc");
            // Where the game keeps its UI atlases and textures; Phantom Liberty's archive is searched first.
            var files = new[] { Path.Combine("ep1", "ep1_2_gamedata.archive"), Path.Combine("content", "basegame_4_gamedata.archive") }
                .Select(a => Path.Combine(archives, a)).Where(File.Exists).Select(p => new GameArchive(p)).ToList();
            return files.Count == 0 ? null : new GameIcons(new GameFiles(files));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The part of an atlas, or null if the game files lack it or can't be read.</summary>
    public GameImage? Icon(ulong atlasPath, string part)
    {
        try
        {
            if (LoadAtlas(atlasPath) is not { } atlas || !atlas.Parts.Parts.TryGetValue(part, out var uv)) return null;

            // The texture is square, so its height equals its width.
            int x = (int)Math.Round(uv.Left * atlas.Width), y = (int)Math.Round(uv.Top * atlas.Width);
            int w = (int)Math.Round(uv.Right * atlas.Width) - x, h = (int)Math.Round(uv.Bottom * atlas.Width) - y;
            return w <= 0 || h <= 0 ? null : new GameImage(w, h, Flip(Bc3.DecodeRegion(atlas.Pixels, atlas.Width, x, atlas.Width - y - h, w, h), w, h));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    static byte[] Flip(byte[] rgba, int w, int h)
    {
        var flipped = new byte[rgba.Length];
        for (var row = 0; row < h; row++) Array.Copy(rgba, row * w * 4, flipped, (h - 1 - row) * w * 4, w * 4);
        return flipped;
    }

    Atlas? LoadAtlas(ulong path)
    {
        if (atlases.TryGetValue(path, out var cached)) return cached;
        Atlas? atlas = null;
        if (data.Read(path) is { } file && InkAtlas.Parse(file) is { } parts && data.Read(parts.Texture) is { } texture && Cr2w.Parse(texture) is { } cr2w)
        {
            var width = cr2w.Object(0).FirstOrDefault(p => p.Name == "width") is { } w ? (int)cr2w.U32At(w.Value) : 0;
            // The pixels are the file's second segment. shortcut: only BC3 (the compression these icon textures use) is decoded, and the texture is assumed square.
            var setup = cr2w.Object(0).FirstOrDefault(p => p.Name == "setup") is { } st ? cr2w.Struct(st.Value, out _) : [];
            var compression = setup.FirstOrDefault(p => p.Name == "compression") is { } c ? cr2w.Name(cr2w.U16(c.Value)) : "";
            if (width > 0 && compression == "TCM_DXTAlpha" && data.Read(GameArchive.PathHash(parts.Texture), 1) is { } pixels && pixels.Length == width * width)
                atlas = new Atlas(parts, width, pixels);
        }
        return atlases[path] = atlas;
    }
}
