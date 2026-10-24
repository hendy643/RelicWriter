using System.ComponentModel;
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
                vm.Exit = Close;
                vm.StartUpdateCheck();
                vm.PropertyChanged += OnViewModelChanged;
            }
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
