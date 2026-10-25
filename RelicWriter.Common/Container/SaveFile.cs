namespace RelicWriter.Common.Container;

/// <summary>Version-independent, lossless view of a sav.dat: header fields plus the raw node tree.</summary>
public sealed class SaveFile
{
    public required SaveHeader Header { get; set; }

    /// <summary>Top-level nodes in file order.</summary>
    public List<SaveNode> Roots { get; } = [];

    public static SaveFile Read(byte[] file) => SaveReader.Read(file);

    public byte[] Write() => SaveWriter.Write(this);

    /// <summary>Depth-first walk, the order the node table uses.</summary>
    public IEnumerable<SaveNode> Walk()
    {
        var stack = new Stack<SaveNode>(Enumerable.Reverse(Roots));
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            for (var i = n.Children.Count - 1; i >= 0; i--) stack.Push(n.Children[i]);
        }
    }
}
