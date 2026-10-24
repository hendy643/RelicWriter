namespace RelicWriter.Common.Codecs;

/// <summary>What a codec needs to decide its version gates (the spec's v and a).</summary>
public readonly record struct SaveContext(uint SaveVersion, uint GameVersion, uint ArchiveVersion);
