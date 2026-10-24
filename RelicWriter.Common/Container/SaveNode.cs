namespace RelicWriter.Common.Container;

/// <summary>One node of the save's node tree. Payloads stay raw until a codec for the node exists.</summary>
public sealed class SaveNode
{
    public required string Name { get; init; }

    /// <summary>The u32 that starts every node's payload.</summary>
    public uint Id { get; set; }

    /// <summary>Payload after the node id and before the first child (the whole payload for a leaf).</summary>
    public byte[] Data { get; set; } = [];

    public List<SaveNode> Children { get; } = [];

    /// <summary>Payload written after the last child (for example TimeSystem, GPSSystem); empty for most nodes.</summary>
    public byte[] Trailing { get; set; } = [];

    /// <summary>Bytes between this node's recorded range and the next sibling (outside any node's size, for example the itemData gaps); empty for most nodes.</summary>
    public byte[] After { get; set; } = [];
}
