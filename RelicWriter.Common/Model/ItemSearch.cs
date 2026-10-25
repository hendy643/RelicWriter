using RelicWriter.Common.GameData;

namespace RelicWriter.Common.Model;

/// <summary>What the item picker shows: the words typed and the filters. A null filter means any.</summary>
public sealed record ItemFilter(string Query = "", string? Category = null, string? Type = null, string? Quality = null, bool Expansion = true);

/// <summary>Case-insensitive fuzzy search over the item catalogue.</summary>
public static class ItemSearch
{
    const int MinFuzzyLength = 3;

    /// <summary>
    /// The items that pass the filters and match every word of the query, best first. A word matches the name as a word start (best), anywhere in it, as letters in order
    /// ("ktna" finds Katana) or within one typo of a word of the name; it also matches the type, category and quality labels, so "rare pistol" works.
    /// </summary>
    public static IEnumerable<ItemInfo> Find(IEnumerable<ItemInfo> items, ItemFilter filter)
    {
        var words = filter.Query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return items
            .Where(i => (filter.Expansion || !i.Expansion) && (filter.Category is null || i.Category == filter.Category) && (filter.Type is null || i.Type == filter.Type) && (filter.Quality is null || i.Quality == filter.Quality))
            .Select(i => (Item: i, Score: Score(i, words)))
            .Where(p => p.Score > 0)
            .OrderByDescending(p => p.Score).ThenBy(p => p.Item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Item.Quality, StringComparer.Ordinal)
            .Select(p => p.Item);
    }

    /// <summary>Whether every word of the query matches the name, in the fuzzy way <see cref="Find"/> matches.</summary>
    public static bool Matches(string name, string query) => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).All(w => WordScore(name, w) > 0);

    // 1 when there is nothing to match (everything passes), 0 when a word matches nothing.
    static int Score(ItemInfo item, string[] words)
    {
        var total = 1;
        foreach (var word in words)
        {
            var s = WordScore(item.Name, word);
            if (s == 0 && $"{item.Type} {item.Category} {item.Quality}".Contains(word, StringComparison.OrdinalIgnoreCase)) s = 1;
            if (s == 0) return 0;
            total += s;
        }
        return total;
    }

    static int WordScore(string name, string word)
    {
        var at = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (at >= 0) return at == 0 || !char.IsLetterOrDigit(name[at - 1]) ? 6 : 4;
        if (word.Length < MinFuzzyLength) return 0;
        if (IsSubsequence(word, name)) return 2;
        return name.Split([' ', '-', ':', '\'', '’'], StringSplitOptions.RemoveEmptyEntries).Any(w => WithinOneTypo(word, w)) ? 2 : 0;
    }

    static bool IsSubsequence(string word, string name)
    {
        var i = 0;
        foreach (var c in name)
            if (i < word.Length && char.ToLowerInvariant(c) == char.ToLowerInvariant(word[i])) i++;
        return i == word.Length;
    }

    // One substitution, insertion, deletion or swap of neighbours.
    static bool WithinOneTypo(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1) return false;
        var i = 0;
        while (i < a.Length && i < b.Length && char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i])) i++;
        if (i == a.Length && i == b.Length) return true;
        bool Same(string x, int xi, string y, int yi) => string.Compare(x, xi, y, yi, int.MaxValue, StringComparison.OrdinalIgnoreCase) == 0;
        if (a.Length == b.Length)
            return Same(a, i + 1, b, i + 1) || (i + 1 < a.Length && char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i + 1]) && char.ToLowerInvariant(a[i + 1]) == char.ToLowerInvariant(b[i]) && Same(a, i + 2, b, i + 2));
        return a.Length > b.Length ? Same(a, i + 1, b, i) : Same(a, i, b, i + 1);
    }
}
