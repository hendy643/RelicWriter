using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RelicWriter.Views;

/// <summary>Shows the generated bug report so the user can copy it into a GitHub issue.</summary>
public partial class BugReportWindow : Window
{
    public BugReportWindow() => InitializeComponent();

    public BugReportWindow(string text) : this() => ReportText.Text = text;

    async void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is null) return;
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(ReportText.Text ?? ""));
        await Clipboard.SetDataAsync(data);
        CopiedNote.Text = "Copied.";
    }

    void OnClose(object? sender, RoutedEventArgs e) => Close();
}
