using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1.Views;

public partial class LogsPage : ContentPage
{
    private readonly LogsViewModel _viewModel;

    public LogsPage(LogsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is BaseViewModel vm)
        {
            vm.IsLoading = true;
            try
            {
                await vm.LoadAsync();
            }
            finally
            {
                vm.IsLoading = false;
            }
        }
    }
}
