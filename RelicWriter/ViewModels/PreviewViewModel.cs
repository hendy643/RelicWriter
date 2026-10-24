namespace RelicWriter.ViewModels;

/// <summary>What the character preview window shows. The 3D view itself is still being built; until it is, the window says so and where the game files were found.</summary>
public sealed class PreviewViewModel(AppearanceViewModel appearance) : ViewModelBase
{
    public string Message => "The 3D preview is not ready yet. It will show V here, built from models read from your game files.";

    public string GameFolderText => appearance.GameFolderText;
}
