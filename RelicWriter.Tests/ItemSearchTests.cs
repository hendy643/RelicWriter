using Xunit;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;

namespace RelicWriter.Tests;

public class ItemSearchTests
{
    static ItemInfo Item(string name, string category, string? type, string? quality, bool expansion = false) =>
        new((uint)name.GetHashCode(), 10, name, category, type, quality, ItemShape.Gear, expansion);

    static readonly ItemInfo[] Items =
    [
        Item("Katana", "Weapon", "Katana", "Rare"), Item("Sasai Kiru", "Weapon", "Katana", "Legendary"), Item("Overture", "Weapon", "Revolver", "Epic"),
        Item("Dying Night", "Weapon", "Handgun", "Legendary", expansion: true), Item("Militech Shoes", "Clothing", "Feet", null), Item("Kerenzikov", "Cyberware", null, "Rare"),
    ];

    static string[] Names(ItemFilter f) => ItemSearch.Find(Items, f).Select(i => i.Name).ToArray();

    [Fact]
    public void EmptyQueryListsEverythingAndFiltersNarrowIt()
    {
        Assert.Equal(6, Names(new()).Length);
        Assert.Equal(["Katana", "Sasai Kiru"], Names(new(Category: "Weapon", Type: "Katana")).Order());
        Assert.Equal(["Kerenzikov", "Katana"], Names(new(Quality: "Rare")).OrderDescending());
        Assert.DoesNotContain("Dying Night", Names(new(Expansion: false)));
    }

    [Theory]
    [InlineData("KATANA", "Katana")]          // case does not matter
    [InlineData("kiru", "Sasai Kiru")]        // a word of the name
    [InlineData("ktna", "Katana")]            // letters in order
    [InlineData("kerenzkov", "Kerenzikov")]   // one letter missing
    [InlineData("overtrue", "Overture")]      // one letter wrong... swapped
    [InlineData("legendary handgun", "Dying Night")] // words of the labels
    public void FuzzyQueryFindsTheItem(string query, string expected) => Assert.Contains(expected, Names(new(query)));

    [Fact]
    public void BetterMatchesComeFirstAndNonMatchesAreDropped()
    {
        Assert.Equal("Katana", Names(new("katana"))[0]);
        Assert.Empty(Names(new("zzzz")));
        Assert.DoesNotContain("Militech Shoes", Names(new("katana")));
    }
}
