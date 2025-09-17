using CommunityToolkit.Mvvm.ComponentModel;

namespace ZebraSCannerTest1.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string title;
}
