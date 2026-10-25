using System.Text.RegularExpressions;
using RelicWriter.Common.GameData;

namespace RelicWriter.Common.Model;

/// <summary>
/// Turns a TweakDBID (CRC-32 + name length, as stored in saves) back into a record name such as "Items.Preset_Yukimura_Default", and gives it a
/// readable name. Both come from the game database: the in-game name where the game has one (variants take their parent record's name), else
/// a name from the CP2077SaveEditor and CyberCAT projects (both MIT), else a readable form of the record name.
/// </summary>
public static partial class RecordNames
{
    /// <summary>The record name, or null when the database doesn't know the id (for example an item added by a mod).</summary>
    public static string? Record(uint hash, byte length) => GameDatabase.Current.Record(hash, length)?.Name;

    /// <summary>The name the game itself gives the record, or null.</summary>
    public static string? GameName(uint hash, byte length) => GameDatabase.Current.Record(hash, length)?.DisplayName;

    /// <summary>The name to show (an item the record table lacks takes its catalogue name), or null when nothing is known about the id.</summary>
    public static string? Display(uint hash, byte length) => GameDatabase.Current.Record(hash, length) is { } known ? known.DisplayName ?? Humanize(known.Name) : GameDatabase.Current.Item(hash, length)?.Name;

    /// <summary>"Items.Preset_Yukimura_Default" becomes "Yukimura Default".</summary>
    public static string Humanize(string record)
    {
        var name = record[(record.IndexOf('.') + 1)..];
        name = CamelBoundary().Replace(name, "$1 $2");
        var words = name.Split(['_', '.', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Where((w, i) => !(i == 0 && w.Equals("Preset", StringComparison.OrdinalIgnoreCase)))
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        var text = string.Join(' ', words);
        return text.Length > 0 ? text : record;
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundary();
}
