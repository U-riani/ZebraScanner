using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class InventorizationByLootsPage : ContentPage
{
    private readonly InventorizationByLootsViewModel _vm;
    private bool _isOpeningScanner;
    public InventorizationByLootsPage(InventorizationByLootsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadLootsAsync();
    }

    private async void OnScanBoxEntryTapped(object sender, TappedEventArgs e)
    {
        if (_isOpeningScanner)
            return;

        _isOpeningScanner = true;

        try
        {
            await Shell.Current.GoToAsync(nameof(LootsScanningPage), true,
                new Dictionary<string, object>
                {
                    ["BoxId"] = string.Empty,
                    ["OpenSetBox"] = true
                });
        }
        finally
        {
            _isOpeningScanner = false;
        }

    }
}
