using CommunityToolkit.Mvvm.Messaging;
using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class LootsScanningPage : ContentPage
{
    private readonly LootsScanningViewModel _vm;

    public LootsScanningPage(LootsScanningViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnBarcodeCompleted(object sender, EventArgs e)
    {
        if (BindingContext is LootsScanningViewModel vm)
        {
            var text = lootBarcodeEntry.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            await _vm.AddProductAsync(text);
            lootBarcodeEntry.Text = string.Empty;

            MainThread.BeginInvokeOnMainThread(() => lootBarcodeEntry.Focus());
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Wait until the viewmodel is fully ready
        await _vm.InitializeAsync();

        // Optionally focus the scanner entry if it exists in XAML
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (this.FindByName<Entry>("lootBarcodeEntry") is Entry entry)
            {
                entry.Focus();
            }
        });
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(_vm);
    }

}

