using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class InventorizationByLootsPage : ContentPage
{
    private readonly InventorizationByLootsViewModel _vm;

    public InventorizationByLootsPage(InventorizationByLootsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _ = _vm.LoadLootsAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadLootsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Dispose();
    }

}
