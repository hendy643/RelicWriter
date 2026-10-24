using RelicWriter.GameAssets;
using RelicWriter.Model3D.Resources;

namespace RelicWriter.Model3D;

/// <summary>The game's files by depot path (for example base\characters\...\x.mesh), searched Phantom Liberty first.</summary>
public sealed class GameResources(GameFiles files)
{
    readonly Dictionary<string, RedFile?> cache = new(StringComparer.OrdinalIgnoreCase);

    public GameFiles Files { get; } = files;

    /// <summary>Opens a game directory's archives, or null when the Oodle library isn't there (Windows only for now).</summary>
    public static GameResources? Open(string gameDir)
    {
        if (!Oodle.TryLoad(gameDir)) return null;
        var root = Path.Combine(gameDir, "archive", "pc");
        if (!Directory.Exists(root)) return null;
        // ep1 (Phantom Liberty) first so its files replace the base game's.
        var archives = Directory.GetFiles(root, "*.archive", SearchOption.AllDirectories)
            .OrderByDescending(p => p.Contains(Path.DirectorySeparatorChar + "ep1" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Select(p => new GameArchive(p));
        return new GameResources(new GameFiles(archives));
    }

    public byte[]? Bytes(string path, int segment = 0) => Files.Read(path, segment);

    public RedFile? File(string path)
    {
        if (cache.TryGetValue(path, out var cached)) return cached;
        return cache[path] = Bytes(path) is { } data ? RedFile.Parse(data) : null;
    }

    /// <summary>The bytes of a deferred buffer stored in the file's later segments.</summary>
    public byte[]? Buffer(string path, RedBuffer buffer) => buffer.Segment is { } n ? Bytes(path, n) : null;
}
