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

        lootBarcodeEntry.Loaded += (s, e) => FocusScannerEntry();
        lootBarcodeEntry.Completed += OnBarcodeCompleted;
    }

    private async void OnBarcodeCompleted(object sender, EventArgs e)
    {
        var text = lootBarcodeEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            FocusScannerEntry();
            return;
        }

        await _vm.AddProductAsync(text);
        lootBarcodeEntry.Text = string.Empty;
        FocusScannerEntry();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LootsScanningViewModel vm)
        {
            await Task.Yield(); // allow QueryProperty binding to finish
            await vm.InitializeAsync();
            FocusScannerEntry();
        }
    }

    /// <summary>
    /// Forcefully refreshes and re-focuses scanner Entry.
    /// </summary>
    private void FocusScannerEntry()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (lootBarcodeEntry == null) return;
            lootBarcodeEntry.IsEnabled = false;
            lootBarcodeEntry.IsEnabled = true;
            lootBarcodeEntry.Focus();
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(_vm);
    }
}
