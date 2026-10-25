using CommunityToolkit.Mvvm.ComponentModel;
using RelicWriter.Common.Model;

namespace RelicWriter.ViewModels;

/// <summary>The game patch (and whether the expansion is included) that the editor currently allows edits for; shared by the tabs, which re-read their limits when it changes.</summary>
public sealed partial class EditionViewModel(GameEdition initial) : ViewModelBase
{
    [ObservableProperty] public partial GameEdition Edition { get; set; } = initial;
}
