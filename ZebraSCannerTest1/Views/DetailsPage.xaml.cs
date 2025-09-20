using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1.Views;

public partial class DetailsPage : ContentPage
{
    private readonly DetailsViewModel _vm;

    public DetailsPage(DetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // ? Remove _vm.Load();
        // Data is already provided by Shell query binding
    }
}
