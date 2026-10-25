using System.Reflection;

namespace RelicWriter.Common.AppInfo;

public static class AppVersion
{
    // Written into every assembly's metadata by the pre-build version sync from version.json.
    static string? Metadata(string key) =>
        typeof(AppVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;

    /// <summary>The version of the application ("app" in version.json when this build was made).</summary>
    public static string Text { get; } = Metadata("AppVersion") is { Length: > 0 } version ? version : "0.0.0";

    /// <summary>The short hash of the commit a build that is not a formal release was made from; null for a formal release.</summary>
    public static string? Revision { get; } = Metadata("GitHash") is { Length: > 0 } hash ? hash : null;

    /// <summary>The version as shown to the user: "1.0.0" for a release, "1.0.0+b0ac55d" for any other build.</summary>
    public static string DisplayText => Revision is null ? Text : $"{Text}+{Revision}";

    /// <summary>The save version the game database was designed for ("database" in version.json); it is the newest save version the built-in data covers.</summary>
    public static uint DatabaseVersion { get; } = uint.TryParse(Metadata("DatabaseVersion"), out var v) ? v : 0;
}
