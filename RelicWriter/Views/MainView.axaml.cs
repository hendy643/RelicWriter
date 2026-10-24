using Avalonia.Controls;
using RelicWriter.ViewModels;

namespace RelicWriter.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Tabs.SelectionChanged += (_, _) => Report();
        DataContextChanged += (_, _) => Report();
    }

    /// <summary>Tells the view model whether the Appearance tab is the one showing, which opens and closes the preview window.</summary>
    void Report()
    {
        if (DataContext is MainViewModel vm) vm.AppearanceTabSelected = ReferenceEquals(Tabs.SelectedItem, AppearanceTab);
    }
}
