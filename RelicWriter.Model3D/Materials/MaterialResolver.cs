using RelicWriter.Model3D.Resources;

namespace RelicWriter.Model3D.Materials;

/// <summary>A material with its inheritance chain flattened: the master template it ends in, and every parameter with the most-derived value.</summary>
public sealed class Material
{
    /// <summary>Depot path of the master template (.mt) at the end of the chain, or null when the chain ended on a missing file.</summary>
    public string? Master { get; init; }

    /// <summary>Parameter name to value: a <see cref="RedRef"/> for textures, double/long/RedStruct for the rest.</summary>
    public required Dictionary<string, object?> Parameters { get; init; }

    public string? Texture(string name) => Parameters.GetValueOrDefault(name) is RedRef { Path: { } path } ? path : null;
}

/// <summary>Follows a material instance (.mi) up its baseMaterial chain; a parameter set closer to the start overrides one further up.</summary>
public static class MaterialResolver
{
    /// <summary>Longest chain followed; the game's skin chains are a handful deep, and a loop would otherwise never end.</summary>
    const int MaxDepth = 16;

    const string MaterialInstance = "CMaterialInstance";

    public static Material Resolve(GameResources res, RedFile start)
    {
        var parameters = new Dictionary<string, object?>();
        string? master = null;
        var file = start;
        for (var depth = 0; depth < MaxDepth && file is not null; depth++)
        {
            var instance = file.Objects.FirstOrDefault(o => o.ClassName == MaterialInstance);
            if (instance is null) break;
            foreach (var entry in instance.List("values").OfType<RedStruct>())
                foreach (var (name, value) in entry.Properties) parameters.TryAdd(name, value);

            if (instance.Get<RedRef>("baseMaterial") is not { Path: { } next }) break;
            if (!next.EndsWith(".mi", StringComparison.OrdinalIgnoreCase)) { master = next; break; }
            file = res.File(next);
        }
        return new Material { Master = master, Parameters = parameters };
    }
}
