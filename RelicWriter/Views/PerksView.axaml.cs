using Avalonia.Controls;
using Avalonia.Input;
using RelicWriter.ViewModels;

namespace RelicWriter.Views;

public partial class PerksView : UserControl
{
    public PerksView()
    {
        InitializeComponent();
    }

    // Click buys a level, right-click sells one; the pointer over a perk shows it on the detail card.
    void NodePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: PerkViewModel perk }) return;
        var point = e.GetCurrentPoint((Control)sender).Properties;
        if (point.IsLeftButtonPressed) perk.Raise();
        else if (point.IsRightButtonPressed) perk.Lower();
    }

    void NodeEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: PerkViewModel perk } && DataContext is PerksViewModel perks) perks.SelectedPerk = perk;
    }
}
