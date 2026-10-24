namespace RelicWriter.Common.Updates;

/// <summary>
/// A version such as 1.2.3 or 1.2.3-beta.2, ordered as semver.org says: the numbers first, and a pre-release is lower than the same numbers without one
/// (1.2.0-rc.1 &lt; 1.2.0). Pre-releases compare part by part split at dots: numbers numerically, text as text, a number lower than text, and fewer parts
/// lower when all earlier parts are equal. Build metadata (<c>+abc</c>) is ignored.
/// </summary>
public sealed record SemVer(Version Number, string? PreRelease = null) : IComparable<SemVer>
{
    /// <summary>"1.2.3", "v1.2.3", "1.2.3-beta" or "1.2.3-beta.2+build5"; null for anything else. Missing parts are zero ("1.2" is 1.2.0).</summary>
    public static SemVer? Parse(string? text)
    {
        if (text is null) return null;
        var s = text.Trim().TrimStart('v', 'V');
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        var dash = s.IndexOf('-');
        var pre = dash < 0 ? null : s[(dash + 1)..];
        if (pre is { Length: 0 }) return null;
        if (!Version.TryParse(dash < 0 ? s : s[..dash], out var number)) return null;
        return new SemVer(new Version(number.Major, number.Minor, Math.Max(number.Build, 0)), pre);
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null) return 1;
        var byNumber = Number.CompareTo(other.Number);
        if (byNumber != 0) return byNumber;
        if (PreRelease is null || other.PreRelease is null) return (PreRelease is null ? 1 : 0) - (other.PreRelease is null ? 1 : 0);

        var a = PreRelease.Split('.');
        var b = other.PreRelease.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aNumeric = ulong.TryParse(a[i], out var an);
            var bNumeric = ulong.TryParse(b[i], out var bn);
            var c = aNumeric && bNumeric ? an.CompareTo(bn) : aNumeric ? -1 : bNumeric ? 1 : string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }

    public static bool operator <(SemVer left, SemVer right) => left.CompareTo(right) < 0;
    public static bool operator >(SemVer left, SemVer right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;

    public override string ToString() => PreRelease is null ? Number.ToString(3) : $"{Number.ToString(3)}-{PreRelease}";
}
