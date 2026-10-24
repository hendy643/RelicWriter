namespace RelicWriter.Common.Container;

/// <summary>Save versions at which the layout of something changes (see docs/save-versions.md; gates marked "game" come from the game's loaders, the others from saves).</summary>
public static class SaveVersions
{
    /// <summary>The oldest save version observed (game 1.03).</summary>
    public const uint Oldest = 193;

    /// <summary>From here a node-level CName is a u64 hash; below it, a string.</summary>
    public const uint CNameIsHash = 193;

    /// <summary>Item ids in the inventory grow with the version: a kind byte from 97, two more bytes from 190, one more from 221 (game).</summary>
    public const uint ItemKindByte = 97, ItemExtraBytes = 190, ItemTrailingByte = 221;

    /// <summary>The vehicle garage is in PersistencySystem2 from here; older saves keep it in PSData (game).</summary>
    public const uint GarageInPersistencySystem2 = 205;
}
