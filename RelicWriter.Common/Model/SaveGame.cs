using RelicWriter.Common.Codecs;
using RelicWriter.Common.Codecs.Generated;
using RelicWriter.Common.Container;

namespace RelicWriter.Common.Model;

/// <summary>
/// Domain view of a save: one shape for every save version. The UI binds to this; <see cref="Load"/> and <see cref="ApplyTo"/>
/// go through the generated node codecs, so nodes without a codec are untouched in the underlying <see cref="SaveFile"/>.
/// </summary>
public sealed class SaveGame
{
    public uint SaveVersion { get; init; }

    public uint GameVersion { get; init; }

    public ushort? UniqueItemCounter { get; set; }

    public uint? PlaythroughSeed { get; set; }

    /// <summary>Raw 32-byte state of the local player node (probably position and orientation; not verified).</summary>
    public LocalPlayer? Player { get; set; }

    public static SaveGame Load(SaveFile file)
    {
        var ctx = Context(file);
        var game = new SaveGame { SaveVersion = file.Header.SaveVersion, GameVersion = file.Header.GameVersion };
        if (Find(file, UniqueItemCounterCodec.Node) is { } counter) game.UniqueItemCounter = new UniqueItemCounterCodec().Read(counter.Data, ctx).Counter;
        if (Find(file, LootManagerPlaythroughSeedCodec.Node) is { } seed) game.PlaythroughSeed = new LootManagerPlaythroughSeedCodec().Read(seed.Data, ctx).Seed;
        if (Find(file, LocalPlayerCodec.Node) is { } player) game.Player = new LocalPlayerCodec().Read(player.Data, ctx);
        return game;
    }

    /// <summary>Writes the domain values back into the raw nodes; a null value leaves its node as it is.</summary>
    public void ApplyTo(SaveFile file)
    {
        var ctx = Context(file);
        if (UniqueItemCounter is { } counter && Find(file, UniqueItemCounterCodec.Node) is { } c) c.Data = new UniqueItemCounterCodec().Write(new UniqueItemCounter { Counter = counter }, ctx);
        if (PlaythroughSeed is { } seed && Find(file, LootManagerPlaythroughSeedCodec.Node) is { } s) s.Data = new LootManagerPlaythroughSeedCodec().Write(new LootManagerPlaythroughSeed { Seed = seed }, ctx);
        if (Player is { } player && Find(file, LocalPlayerCodec.Node) is { } p) p.Data = new LocalPlayerCodec().Write(player, ctx);
    }

    static SaveContext Context(SaveFile f) => new(f.Header.SaveVersion, f.Header.GameVersion, f.Header.ArchiveVersion);

    static SaveNode? Find(SaveFile file, string name) => file.Walk().FirstOrDefault(n => n.Name == name);
}
