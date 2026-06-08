using CommunityToolkit.Mvvm.Messaging;
using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class LootsScanningPage : ContentPage
{
    private readonly LootsScanningViewModel _vm;
    private CancellationTokenSource? _focusCts;
    private bool _isActive;

    public LootsScanningPage(LootsScanningViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        lootBarcodeEntry.Loaded += (_, _) => QueueScannerFocus();
    }

    private async void OnBarcodeCompleted(object sender, EventArgs e)
    {
        var text = lootBarcodeEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ClearBarcodeInput();
            QueueScannerFocus();
            return;
        }

        await _vm.AddProductAsync(text);
        ClearBarcodeInput();
        QueueScannerFocus();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isActive = true;

        if (BindingContext is LootsScanningViewModel vm)
        {
            await Task.Yield(); // allow QueryProperty binding to finish

            // Do not block Shell navigation while SQLite loads the recent rows.
            // The page must render first; data can appear a moment later without freezing Android.
            _ = vm.InitializeAsync();
            QueueScannerFocus(attempts: 4);
        }
    }

    private void ClearBarcodeInput()
    {
        lootBarcodeEntry.Text = string.Empty;
        _vm.CurrentBarcode = string.Empty;
    }

    /// <summary>
    /// Keeps the scanner Entry focused after barcode submit without using the old heavy
    /// disable/enable trick. A few short retries are intentional: CollectionView row updates
    /// can steal focus during layout on Android/Zebra devices.
    /// </summary>
    private void QueueScannerFocus(int attempts = 3)
    {
        if (!_isActive && lootBarcodeEntry.IsLoaded)
            return;

        _focusCts?.Cancel();
        _focusCts?.Dispose();
        _focusCts = new CancellationTokenSource();
        var token = _focusCts.Token;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            for (var i = 0; i < attempts; i++)
            {
                if (token.IsCancellationRequested || !_isActive)
                    return;

                await Task.Delay(i == 0 ? 35 : 90);

                if (token.IsCancellationRequested || !_isActive || lootBarcodeEntry == null)
                    return;

                lootBarcodeEntry.Text = string.Empty;
                _vm.CurrentBarcode = string.Empty;
                lootBarcodeEntry.Focus();
                lootBarcodeEntry.CursorPosition = lootBarcodeEntry.Text?.Length ?? 0;
            }
        });
    }

    protected override void OnDisappearing()
    {
        _isActive = false;
        _focusCts?.Cancel();
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(_vm);
    }
}
