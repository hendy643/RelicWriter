using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RelicWriter.Common.Model;

namespace RelicWriter.ViewModels;

public partial class ItemViewModel : ViewModelBase
{
    public const decimal MaxStack = 999_999;

    readonly ItemEntry entry;
    Action? changed;

    public ItemViewModel(ItemEntry entry, Action changed)
    {
        this.entry = entry;
        Quantity = entry.Quantity;
        this.changed = changed;
    }

    public bool IsMoney => entry.IsMoney;

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

/// <summary>The inventory screen: eddies, a selectable sub-inventory and a grid of item tiles.</summary>
public partial class InventoryViewModel : ViewModelBase
{
    public const int MaxSearchLength = 32;

    readonly ItemViewModel? money;

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

    public InventoryViewModel(Inventory inventory, Action changed)
    {
        var unnamed = 0;
        Subs = inventory.SubInventories
            .Select(s => new SubInventoryViewModel($"{(KnownNames.TryGetValue(s.Id, out var n) ? n : "Other storage " + ++unnamed)}  ·  {s.Items.Count} items", s.Items.Select(e => new ItemViewModel(e, changed)).ToList()))
            .ToList();
        money = Subs.SelectMany(s => s.Items).FirstOrDefault(i => i.IsMoney);
        Money = money?.Quantity;
        // Start on the sub-inventory that holds the player's eddies.
        SelectedSub = money is null ? Subs.FirstOrDefault() : Subs.First(s => s.Items.Contains(money));
    }

    public IReadOnlyList<SubInventoryViewModel> Subs { get; }

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    public bool CanEditMoney => money is not null;

    public decimal MaxMoney => int.MaxValue;

    [ObservableProperty] public partial decimal? Money { get; set; }
    [ObservableProperty] public partial SubInventoryViewModel? SelectedSub { get; set; }
    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial ItemViewModel? Selected { get; set; }

    partial void OnMoneyChanged(decimal? value)
    {
        if (money is null || value is null) return;
        money.Quantity = value;
    }

    partial void OnSelectedSubChanged(SubInventoryViewModel? value) => Refresh();

    partial void OnSearchChanged(string value) => Refresh();

    void Refresh()
    {
        Items.Clear();
        if (SelectedSub is null) return;
        var q = Search.Trim();
        foreach (var item in SelectedSub.Items)
            if (q.Length == 0 || item.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                Items.Add(item);
        Selected = Items.FirstOrDefault();
    }
}
