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

        if (_viewModel == null || _viewModel.IsLoading)
            return;

        _viewModel.IsLoading = true;

        try
        {
            await Task.Delay(300);
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            _viewModel.IsLoading = false;
        }
    }

}
