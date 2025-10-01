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

}
