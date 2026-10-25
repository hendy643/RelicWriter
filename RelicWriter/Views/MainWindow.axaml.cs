using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RelicWriter.ViewModels;

namespace RelicWriter.Views;

public partial class MainWindow : Window, IFileDialogs
{
    static readonly FilePickerFileType SaveFiles = new("Cyberpunk 2077 save") { Patterns = ["sav.dat", "*.dat"] };

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Dialogs = this;
                vm.PropertyChanged += OnViewModelChanged;
            }
        };
        // Made for 1080p with the taskbar showing (1040 px usable, about 1000 inside the title bar); on a smaller screen it shrinks to fit.
        Opened += (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is not { } screen) return;
            Width = Math.Min(Width, screen.WorkingArea.Width / screen.Scaling - 16);
            Height = Math.Min(Height, screen.WorkingArea.Height / screen.Scaling - 40);
            Position = new PixelPoint(screen.WorkingArea.X + (int)((screen.WorkingArea.Width - Width * screen.Scaling) / 2), screen.WorkingArea.Y + (int)((screen.WorkingArea.Height - Height * screen.Scaling) / 2));
        };
        PositionChanged += (_, _) => { if (preview is { IsVisible: true }) preview.PlaceBeside(this); };
        Closed += (_, _) => preview?.CloseForReal();
    }

    PreviewWindow? preview;

    void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ShowPreview) && e.PropertyName != nameof(MainViewModel.Preview)) return;
        if (DataContext is not MainViewModel vm) return;
        if (vm.ShowPreview)
        {
            preview ??= new PreviewWindow();
            preview.DataContext = vm.Preview;
            preview.PlaceBeside(this);
            if (!preview.IsVisible) preview.Show(this);
        }
        else preview?.Hide();
    }

    public async Task<string?> PickOpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open a Cyberpunk 2077 save (sav.dat)", AllowMultiple = false, FileTypeFilter = [SaveFiles] });
        return files.Count == 1 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select the Cyberpunk 2077 folder", AllowMultiple = false });
        return folders.Count == 1 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveAsync(string suggestedFileName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save as", SuggestedFileName = suggestedFileName, FileTypeChoices = [SaveFiles] });
        return file?.TryGetLocalPath();
    }
}
