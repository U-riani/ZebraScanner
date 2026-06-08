using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class LootsScanningPage : ContentPage
{
    private readonly LootsScanningViewModel _vm;
    private CancellationTokenSource? _focusCts;
    private bool _isActive;
    private bool _isSettingBox;

    public LootsScanningPage(LootsScanningViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // First page render only. The real focus work is done in OnAppearing.
        // Do not disable/enable the Entry here, because that creates visible blinking.
        lootBarcodeEntry.Loaded += (_, _) => QueueScannerFocus(attempts: 2, firstDelayMs: 120);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isActive = true;

        RegisterFocusAfterLootScanHandler();
        _ = InitializeAndMaybeOpenSetBoxAsync(_vm);
    }

    private void RegisterFocusAfterLootScanHandler()
    {
        WeakReferenceMessenger.Default.Unregister<ProductUpdatedMessage>(this);
        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (_, msg) =>
        {
            if (!_isActive)
                return;

            if (msg.Mode.HasValue && msg.Mode.Value != InventoryMode.Loots)
                return;

            var currentBoxId = _vm.CurrentBoxId?.Trim() ?? string.Empty;
            var productBoxId = msg.Product.Box_Id?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(productBoxId)
                && !string.Equals(productBoxId, currentBoxId, StringComparison.OrdinalIgnoreCase))
                return;

            // The CollectionView redraw happens after the SQLite/background queue updates the row.
            // Refocus after that redraw; otherwise Android/Zebra may leave focus on the row.
            QueueScannerFocus(attempts: 4, firstDelayMs: 160);
        });
    }

    private async void OnBarcodeCompleted(object sender, EventArgs e)
    {
        var text = lootBarcodeEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ClearBarcodeInput();
            QueueScannerFocus(attempts: 2, firstDelayMs: 60);
            return;
        }

        await _vm.AddProductAsync(text);
        ClearBarcodeInput();

        // Quick focus for fast scanners. ProductUpdatedMessage will do the final focus after row redraw.
        QueueScannerFocus(attempts: 2, firstDelayMs: 40);
    }

    private async void OnSetBoxClicked(object sender, EventArgs e)
    {
        if (_isSettingBox)
            return;

        _isSettingBox = true;
        try
        {
            await _vm.SetBoxCommand.ExecuteAsync(null);
            ClearBarcodeInput();
            QueueScannerFocus(attempts: 4, firstDelayMs: 180);
        }
        finally
        {
            _isSettingBox = false;
        }
    }

    private async Task InitializeAndMaybeOpenSetBoxAsync(LootsScanningViewModel vm)
    {
        await vm.InitializeAsync();

        if (vm.OpenSetBoxOnAppear)
        {
            vm.OpenSetBoxOnAppear = false;

            // Let Shell finish rendering the page before opening the prompt.
            await Task.Delay(180);

            await vm.SetBoxCommand.ExecuteAsync(null);
            ClearBarcodeInput();
            QueueScannerFocus(attempts: 4, firstDelayMs: 180);
            return;
        }

        QueueScannerFocus(attempts: 3, firstDelayMs: 120);
    }

    private void ClearBarcodeInput()
    {
        lootBarcodeEntry.Text = string.Empty;
        _vm.CurrentBarcode = string.Empty;
    }

    /// <summary>
    /// Keeps the scanner Entry focused without disabling/enabling it.
    /// Disabling the Entry fixes focus on some Android builds, but it visibly blinks the input.
    /// This version only calls Focus() when needed and retries after layout redraws.
    /// </summary>
    private void QueueScannerFocus(int attempts = 3, int firstDelayMs = 80)
    {
        if (!_isActive)
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

                await Task.Delay(i == 0 ? firstDelayMs : 120);

                if (token.IsCancellationRequested || !_isActive || lootBarcodeEntry == null)
                    return;

                ClearBarcodeInput();

                // Do not Unfocus/disable first. That causes the visible double blink.
                if (!lootBarcodeEntry.IsFocused)
                    lootBarcodeEntry.Focus();

                lootBarcodeEntry.CursorPosition = lootBarcodeEntry.Text?.Length ?? 0;
            }
        });
    }

    protected override void OnDisappearing()
    {
        _isActive = false;
        _focusCts?.Cancel();

        WeakReferenceMessenger.Default.Unregister<ProductUpdatedMessage>(this);
        WeakReferenceMessenger.Default.UnregisterAll(_vm);

        base.OnDisappearing();
    }
}