using Avalonia;
using Avalonia.Controls;

namespace RelicWriter.Views;

/// <summary>A window beside the main one that is open while the Appearance tab is. Closing it by hand only hides it until the tab is entered again.</summary>
public partial class PreviewWindow : Window
{
    /// <summary>Gap between the main window and the preview, in pixels.</summary>
    const int Gap = 8;

    bool closeForReal;

    public PreviewWindow()
    {
        InitializeComponent();
        Closing += (_, e) =>
        {
            if (closeForReal) return;
            e.Cancel = true;
            Hide();
        };
    }

    public void CloseForReal()
    {
        closeForReal = true;
        Close();
    }

    /// <summary>Puts the window against the main window's right edge, or inside the screen's right edge when there is no room outside.</summary>
    public void PlaceBeside(Window main)
    {
        var scale = main.RenderScaling;
        var width = (int)(Width * scale);
        var x = main.Position.X + (int)(main.Bounds.Width * scale) + Gap;
        var y = main.Position.Y;
        if (main.Screens.ScreenFromWindow(main)?.WorkingArea is { } area)
        {
            if (x + width > area.Right) x = area.Right - width;
            y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - (int)(Height * scale)));
        }
        Position = new PixelPoint(x, y);
    }
}
