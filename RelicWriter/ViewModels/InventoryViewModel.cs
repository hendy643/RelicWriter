using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicWriter.Common.GameData;
using RelicWriter.Common.Model;

namespace RelicWriter.ViewModels;

public partial class ItemViewModel : ViewModelBase
{
    public const decimal MaxStack = 999_999;

    readonly ItemEntry entry;
    Action? changed;

    public ItemEntry Entry => entry;

    /// <summary>The catalogue entry of the item, for its tooltip; null for items the game data does not list (eddies, quest items, mods).</summary>
    public ItemInfo? Info => GameDatabase.Current.Item(entry.RecordHash, entry.RecordLength);

    public ItemViewModel(ItemEntry entry, Action changed)
    {
        this.entry = entry;
        Quantity = entry.Quantity;
        this.changed = changed;
    }

    public bool IsMoney => entry.IsMoney;

    public bool IsQuest => RecordNames.IsQuestItem(entry.RecordHash, entry.RecordLength);

    /// <summary>The save stores only a hash; the name comes from the game files, else from the bundled record-name table.</summary>
    public string Title => RecordNames.Display(entry.RecordHash, (byte)entry.RecordLength) ?? (entry.IsMoney ? "Eddies" : "Unknown item");

    public string Subtitle => entry.Quantity is null ? "GEAR · MODS" : "STACK";

    public bool HasQuantity => entry.Quantity is not null;

    public string QuantityBadge => entry.Quantity is null ? "" : "×" + Quantity?.ToString("N0");

    public decimal MaxQuantity => entry.IsMoney ? int.MaxValue : MaxStack;

    [ObservableProperty] public partial decimal? Quantity { get; set; }

    partial void OnQuantityChanged(decimal? value)
    {
        OnPropertyChanged(nameof(QuantityBadge));
        if (value is null || !HasQuantity) return;
        entry.Quantity = (uint)value.Value;
        changed?.Invoke();
    }
}

public sealed record SubInventoryViewModel(string Name, IReadOnlyList<ItemViewModel> Items)
{
    public override string ToString() => Name;
}

/// <summary>The inventory screen: eddies, a selectable sub-inventory, a grid of item tiles and a picker that adds items from the game's catalogue to V's bag.</summary>
public partial class InventoryViewModel : ViewModelBase
{
    public const int MaxSearchLength = 32;
    public const decimal MaxAddQuantity = 9_999;
    const int MaxResults = 200;
    const string Any = "Any";
    // Quality labels in the order the game ranks them (the catalogue's own).
    static readonly string[] QualityOrder = ["Random", "Common", "Common+", "Uncommon", "Uncommon+", "Rare", "Rare+", "Epic", "Epic+", "Legendary", "Legendary+", "Legendary++"];

    readonly Inventory inventory;
    readonly Action changed;
    readonly EditionViewModel edition;
    readonly IReadOnlyList<ItemInfo> catalogue = [.. GameDatabase.Current.Items.Values.Where(i => !RecordNames.IsQuestItem(i.Hash, i.Length))];
    ItemViewModel? money;

    // Container ids and names as documented by the CyberCAT project (MIT). Not verified against the game; unlisted ones are numbered.
    static readonly Dictionary<ulong, string> KnownNames = new()
    {
        [0x1] = "V's Bag",
        [0xF4240] = "Car Stash",
        [0x895724] = "Nomad Intro Items",
        [0x895956] = "Street Kid Intro Items",
        [0x8959E8] = "Corpo Intro Items",
        [0x38E8D0C9F9A087AE] = "Panam's Stash",
        [0x6E48C594562422DE] = "Judy's Stash",
        [0x7901DE03D136A5AF] = "V's Wardrobe",
        [0xE5F556FCBB62A706] = "V's Stash",
        [0xEDAD8C9B086A615E] = "River's Stash",
    };

    public InventoryViewModel(Inventory inventory, Action changed, EditionViewModel edition)
    {
        this.inventory = inventory;
        this.changed = changed;
        this.edition = edition;
        Categories = [Any, .. catalogue.Select(i => i.Category).Distinct().Order()];
        Qualities = [Any, .. catalogue.Select(i => i.Quality).OfType<string>().Distinct().OrderBy(q => Array.IndexOf(QualityOrder, q))];
        Types = [Any];
        Rebuild(null);
        // Items from the expansion are offered only while the editor allows the expansion.
        edition.PropertyChanged += (_, _) => RefreshResults();
        RefreshResults();
    }

    public IReadOnlyList<SubInventoryViewModel> Subs { get; private set; } = [];

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    public bool CanEditMoney => money is not null;

    public decimal MaxMoney => int.MaxValue;

    [ObservableProperty] public partial decimal? Money { get; set; }
    [ObservableProperty] public partial SubInventoryViewModel? SelectedSub { get; set; }
    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial ItemViewModel? Selected { get; set; }

    // ---- the add-item picker ----

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<string> Qualities { get; }

    [ObservableProperty] public partial IReadOnlyList<string> Types { get; set; }
    [ObservableProperty] public partial string PickerQuery { get; set; } = "";
    [ObservableProperty] public partial string PickerCategory { get; set; } = Any;
    [ObservableProperty] public partial string PickerType { get; set; } = Any;
    [ObservableProperty] public partial string PickerQuality { get; set; } = Any;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddItemCommand)), NotifyPropertyChangedFor(nameof(AddHasQuantity))] public partial ItemInfo? PickerSelected { get; set; }
    [ObservableProperty] public partial decimal? AddQuantity { get; set; } = 1;
    [ObservableProperty] public partial string ResultsText { get; set; } = "";
    [ObservableProperty] public partial string AddStatus { get; set; } = "An added item is a copy of a plain item already in the save with the new record in it. Check it in the game.";

    public ObservableCollection<ItemInfo> Results { get; } = [];

    /// <summary>Gear has no stack size.</summary>
    public bool AddHasQuantity => PickerSelected is { Shape: not ItemShape.Gear };

    partial void OnPickerQueryChanged(string value) => RefreshResults();

    partial void OnPickerQualityChanged(string value) => RefreshResults();

    partial void OnPickerTypeChanged(string value) => RefreshResults();

    partial void OnPickerCategoryChanged(string value)
    {
        Types = [Any, .. catalogue.Where(i => value == Any || i.Category == value).Select(i => i.Type).OfType<string>().Distinct().Order()];
        PickerType = Any;
        RefreshResults();
    }

    void RefreshResults()
    {
        string? Chosen(string v) => v == Any ? null : v;
        var found = ItemSearch.Find(catalogue, new ItemFilter(PickerQuery, Chosen(PickerCategory), Chosen(PickerType), Chosen(PickerQuality), edition.Edition.HasPhantomLiberty)).ToList();
        Results.Clear();
        foreach (var item in found.Take(MaxResults)) Results.Add(item);
        PickerSelected = Results.FirstOrDefault();
        ResultsText = found.Count > MaxResults ? $"{found.Count:N0} items, showing the first {MaxResults}" : $"{found.Count:N0} items";
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    void AddItem()
    {
        if (PickerSelected is not { } item) return;
        if (inventory.Add(item.Hash, item.Length, item.Shape, (uint)(AddQuantity ?? 1)) is not { } entry)
        {
            AddStatus = "This save has no bag, or no item of that kind to copy from, so it cannot take this item.";
            return;
        }
        changed();
        Rebuild(entry);
        AddStatus = $"Added {item.Name} to V's Bag.";
    }

    bool CanAdd() => PickerSelected is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    void DeleteItem()
    {
        if (Selected is not { } item) return;
        var sub = Subs.ToList().IndexOf(SelectedSub!);
        if (!inventory.Remove(item.Entry))
        {
            AddStatus = "This item cannot be deleted (eddies, a quest item, or the last item stored in the save).";
            return;
        }
        changed();
        Rebuild(null);
        SelectedSub = Subs[sub];
        AddStatus = $"Deleted {item.Title}.";
    }

    bool CanDelete() => Selected is { IsMoney: false, IsQuest: false };

    partial void OnSelectedChanged(ItemViewModel? value) => DeleteItemCommand.NotifyCanExecuteChanged();

    partial void OnMoneyChanged(decimal? value)
    {
        if (money is null || value is null) return;
        money.Quantity = value;
    }

    partial void OnSelectedSubChanged(SubInventoryViewModel? value) => Refresh();

    partial void OnSearchChanged(string value) => Refresh();

    // Builds the tiles from the inventory again (needed after an item is added, which reads it again). Shows the bag with the new item selected, else the container that holds the eddies.
    void Rebuild(ItemEntry? added)
    {
        var unnamed = 0;
        Subs = inventory.SubInventories
            .Select(s => new SubInventoryViewModel($"{(KnownNames.TryGetValue(s.Id, out var n) ? n : "Other storage " + ++unnamed)}  ·  {s.Items.Count} items", s.Items.Select(e => new ItemViewModel(e, changed)).ToList()))
            .ToList();
        OnPropertyChanged(nameof(Subs));
        money = Subs.SelectMany(s => s.Items).FirstOrDefault(i => i.IsMoney);
        Money = money?.Quantity;
        OnPropertyChanged(nameof(CanEditMoney));
        Search = "";
        var shown = added is null ? money : Subs.SelectMany(s => s.Items).First(i => i.Entry == added);
        SelectedSub = shown is null ? Subs.FirstOrDefault() : Subs.First(s => s.Items.Contains(shown));
        Refresh();
        if (added is not null) Selected = shown;
    }

    void Refresh()
    {
        Items.Clear();
        if (SelectedSub is null) return;
        var q = Search.Trim();
        foreach (var item in SelectedSub.Items)
            if (ItemSearch.Matches(item.Title, q))
                Items.Add(item);
        Selected = Items.FirstOrDefault();
    }
}
