namespace RelicWriter.Common.AppInfo;

/// <summary>One version's entry in the change log: a heading such as "1.0.0 · 24 October 2026" and its lines. <see cref="ChangelogData"/> is made from CHANGELOG.md at build time.</summary>
public sealed record ChangelogEntry(string Heading, IReadOnlyList<string> Lines);
