using CommunityToolkit.Mvvm.ComponentModel;

namespace ZebraSCannerTest1.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isLoading;

    public virtual Task LoadAsync(Object? parameter = null)
    {
        return Task.CompletedTask;
    }
}
