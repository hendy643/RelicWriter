namespace RelicWriter.Common.Codecs;

/// <summary>Reads and writes the payload of one node name (the bytes after the node id, before the first child). Version gates live inside the codec.</summary>
public interface INodeCodec<T>
{
    T Read(ReadOnlySpan<byte> data, SaveContext ctx);

    byte[] Write(T value, SaveContext ctx);
}
