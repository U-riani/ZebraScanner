using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class TasksPage : ContentPage
{
    private readonly TasksViewModel _viewModel = new();

    public TasksPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var token = await SecureStorage.GetAsync("token");

        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("TOKEN NOT FOUND");
            return;
        }

        await _viewModel.LoadDocuments(token);
    }
}