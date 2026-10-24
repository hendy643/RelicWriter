namespace RelicWriter.Common.Codecs;

/// <summary>A node-level CName: a u64 hash from save version 193, a string below.</summary>
public readonly record struct CNameValue(ulong Hash, string? Text);
