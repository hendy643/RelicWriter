using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicWriter.Common.Model;
using RelicWriter.GameAssets;

namespace RelicWriter.ViewModels;

public partial class VehicleViewModel : ViewModelBase
{
    readonly VehicleGarage? garage;
    readonly VehicleId id;
    readonly Action changed;

    public VehicleViewModel(VehicleGarage? garage, VehicleId id, string? record, Action changed)
    {
        this.garage = garage;
        this.id = id;
        this.changed = changed;
        Record = record ?? RecordNames.Record(id.Hash, id.Length) ?? "Unknown vehicle";
        IsUnlocked = garage?.IsUnlocked(id) ?? false;
        Thumbnail = LoadIcon(record);
    }

    /// <summary>False when the save has no garage this app can read; the vehicle is then listed, unticked.</summary>
    public bool CanEdit => garage is not null;

    public string Record { get; }

    public bool IsBike => Record.Contains("bike", StringComparison.OrdinalIgnoreCase);

    /// <summary>"Vehicle.v_sport2_quadra_type66_nomad_player" becomes "QUADRA TYPE66 NOMAD".</summary>
    public string Title
    {
        get
        {
            var parts = Record.Split('.').Last().Split('_');
            // The game's own model name ("Type-66 Avenger") with the maker from the record name ("quadra") in front.
            if (RecordNames.GameName(id.Hash, id.Length) is { } model)
            {
                var maker = parts.Length >= 4 ? char.ToUpperInvariant(parts[2][0]) + parts[2][1..] : null;
                return maker is null || model.StartsWith(maker, StringComparison.OrdinalIgnoreCase) ? model : $"{maker} {model}";
            }
            // Names are v_<class>_<maker>_<model>..._player; anything that doesn't fit is shown as is.
            if (parts.Length < 3 || parts[0] != "v") return Record.ToUpperInvariant();
            // "type66" reads as "type 66"; short model codes like "r7" are left alone.
            return Regex.Replace(string.Join(' ', parts.Skip(2).Where(p => p != "player")), "([a-z]{3,})(\\d)", "$1 $2").ToUpperInvariant();
        }
    }

    public string Subtitle => IsBike ? "MOTORCYCLE" : "CAR";

    /// <summary>The vehicle's picture from the game files; null (a drawn glyph is shown) when the game isn't found or the catalogue lacks the vehicle.</summary>
    public Bitmap? Thumbnail { get; }

    public bool HasThumbnail => Thumbnail is not null;

    [ObservableProperty] public partial bool IsUnlocked { get; set; }

    partial void OnIsUnlockedChanged(bool value)
    {
        if (garage is null || value == garage.IsUnlocked(id)) return;
        if (value) garage.Unlock(id); else garage.Lock(id);
        changed();
    }

    // The picture is cut out of the player's own game files, where the game database says it is; null (a drawn glyph is shown) without them.
    static Bitmap? LoadIcon(string? record)
    {
        if (record is null || VehicleCatalogue.Entry(record) is not { Atlas: { } atlas, AtlasPart: { } part } || GameIcons.Current?.Icon(atlas, part) is not { } image) return null;
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var frame = bitmap.Lock();
        var rowBytes = image.Width * 4;
        for (var y = 0; y < image.Height; y++) System.Runtime.InteropServices.Marshal.Copy(image.Rgba, y * rowBytes, frame.Address + y * frame.RowBytes, rowBytes);
        return bitmap;
    }
}

/// <summary>The vehicles tab: every vehicle the game can unlock as a tile, ticked when unlocked.</summary>
public partial class VehiclesViewModel : ViewModelBase
{
    public const int MaxSearchLength = 32;

    readonly List<VehicleViewModel> all;
    readonly bool canEdit;

    public VehiclesViewModel(VehicleGarage? garage, Action changed)
    {
        canEdit = garage is not null;
        var known = VehicleCatalogue.Records.ToDictionary(VehicleId.FromName);
        // Vehicles the save has unlocked that the catalogue doesn't list (for example from a later patch) are kept and shown by record id.
        all = VehicleCatalogue.Records.Select(r => new VehicleViewModel(garage, VehicleId.FromName(r), r, changed))
            .Concat((garage?.Unlocked ?? []).Distinct().Where(u => !known.ContainsKey(u)).Select(u => new VehicleViewModel(garage, u, null, changed)))
            .ToList();
        foreach (var v in all) v.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(VehicleViewModel.IsUnlocked)) OnPropertyChanged(nameof(Summary)); };
        Refresh();
    }

    public List<VehicleViewModel> Visible { get; private set; } = [];

    public bool CanEdit => canEdit;

    // Saves before version 205 keep the garage elsewhere, which isn't read; the vehicles are still listed.
    public string Summary => canEdit ? $"{all.Count(v => v.IsUnlocked)} OF {all.Count} UNLOCKED" : "THIS SAVE'S GARAGE CAN'T BE READ";

    [ObservableProperty] public partial string Search { get; set; } = "";

    partial void OnSearchChanged(string value) => Refresh();

    [RelayCommand]
    void UnlockAll()
    {
        if (canEdit) foreach (var v in all) v.IsUnlocked = true;
    }

    void Refresh()
    {
        var q = Search.Trim();
        Visible = all.Where(v => q.Length == 0 || v.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || v.Record.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        OnPropertyChanged(nameof(Visible));
    }
}
