using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Helpers;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels;

[QueryProperty(nameof(CurrentBoxId), "BoxId")]
public partial class LootsScanningViewModel : ObservableObject, IDisposable
{
    private readonly IProductService _productService;
    //private readonly IExcelExportService _exporter;
    //private readonly IExcelExportLogsService _logExporter;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ILoggerService<LootsScanningViewModel> _logger;
    private readonly PopupService _popup;
    private readonly IScanningService _scanningService;

    [ObservableProperty] private string currentBoxId = string.Empty;
    [ObservableProperty] private string currentBarcode = string.Empty;
    [ObservableProperty] private string lastScannedBarcode = string.Empty;
    [ObservableProperty] private string currentBoxDisplay = "No box selected";
    [ObservableProperty] private string boxScanHint = "Scan a box ID first. Product scans start after a box is selected.";
    [ObservableProperty] private bool isWaitingForBoxScan = true;
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<ProductSlot> Slots { get; } =
        new(Enumerable.Range(0, 8).Select(_ => new ProductSlot()));

    public IAsyncRelayCommand<string> AddProductCommand { get; }
    //public IAsyncRelayCommand ExportDataCommand { get; }
    public IAsyncRelayCommand ShowResultsCommand { get; }
    public IAsyncRelayCommand GoToLogsCommand { get; }
    public IAsyncRelayCommand GoToScannedProductsCommand { get; }
    public IAsyncRelayCommand<ProductSlot> GoToDetailsCommand { get; }
    public IRelayCommand ScanBoxCommand { get; }
    public IAsyncRelayCommand SetBoxCommand { get; }
    public IRelayCommand ClearBoxCommand { get; }

    public LootsScanningViewModel(
        IProductService productService,

    IDialogService dialogs,                   // injected
    INavigationService navigation,
    ILoggerService<LootsScanningViewModel> logger,
    PopupService popup,
    IScanningService scanningService)
    {
        _productService = productService;

        _dialogs = dialogs;                        // ← you forgot this
        _navigation = navigation;
        _logger = logger;
        _popup = popup;
        _scanningService = scanningService;

        //_scanningService.StartAsync();

        AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);

        ShowResultsCommand = new AsyncRelayCommand(ShowResultsAsync);

        GoToLogsCommand = new AsyncRelayCommand(() =>
            _navigation.NavigateToAsync(nameof(LogsPage), new Dictionary<string, object>
            {
                ["Mode"] = InventoryMode.Loots,
                ["BoxId"] = CurrentBoxId
            }));

        GoToScannedProductsCommand = new AsyncRelayCommand(() =>
        _navigation.NavigateToAsync(nameof(ScannedProductsPage),
            new Dictionary<string, object>
            {
                ["Mode"] = InventoryMode.Loots,
                ["BoxId"] = CurrentBoxId
            }));

        GoToDetailsCommand = new AsyncRelayCommand<ProductSlot>(async slot =>
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.Barcode) || string.IsNullOrWhiteSpace(CurrentBoxId))
                return;

            // Try load product from DB first, scoped by the active transfer box.
            var product = await _productService.GetByBarcodeAsync(slot.Barcode, InventoryMode.Loots, CurrentBoxId);
            if (product == null)
                return;

            await _navigation.NavigateToAsync(nameof(DetailsPage),
                new Dictionary<string, object>
                {
                    ["Barcode"] = product.Barcode,
                    ["Quantity"] = product.ScannedQuantity,
                    ["InitialQuantity"] = product.InitialQuantity,
                    ["Name"] = product.Name ?? "",
                    ["Color"] = product.Color ?? "",
                    ["Size"] = product.Size ?? "",
                    ["Price"] = decimal.TryParse(product.Price, out var p) ? p : 0,
                    ["ArticCode"] = product.ArticCode ?? "",
                    ["IsReadOnly"] = false,
                    ["Mode"] = InventoryMode.Loots,
                    ["BoxId"] = product.Box_Id ?? CurrentBoxId
                });
        });

        ScanBoxCommand = new RelayCommand(EnableBoxScanMode);
        SetBoxCommand = new AsyncRelayCommand(SetBoxManuallyAsync);
        ClearBoxCommand = new RelayCommand(ClearCurrentBox);

        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(
            this, async (_, _) => await LoadRecentAsync());
    }


    public async Task InitializeAsync()
    {
        WeakReferenceMessenger.Default.Unregister<ProductUpdatedMessage>(this);
        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(
            this, async (_, _) => await LoadRecentAsync());

        if (string.IsNullOrWhiteSpace(CurrentBoxId))
            CurrentBoxId = Preferences.Get(GetCurrentBoxPreferenceKey(), string.Empty);

        SetCurrentBox(CurrentBoxId);
        IsWaitingForBoxScan = string.IsNullOrWhiteSpace(CurrentBoxId);
        RefreshBoxHint();

        _scanningService.StartAsync();

        await LoadRecentAsync();
    }


    public async Task LoadRecentAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentBoxId))
        {
            ClearSlots();
            return;
        }

        var products = (await _productService.GetLootBarcodeProgressByBoxAsync(CurrentBoxId)).ToList();

        for (int i = 0; i < Slots.Count; i++)
        {
            if (i < products.Count)
            {
                var p = products[i];
                Slots[i].SetProgress(
                    barcode: p.Barcode,
                    currentBoxScanned: p.CurrentBoxScannedQuantity,
                    currentBoxExpected: p.CurrentBoxExpectedQuantity,
                    barcodeTotalScanned: p.BarcodeTotalScannedQuantity,
                    barcodeTotalExpected: p.BarcodeTotalExpectedQuantity,
                    usesBarcodeTotalFallback: p.UsesBarcodeTotalFallback,
                    hasKnownExpectedQuantity: p.UsesBarcodeTotalFallback || p.CurrentBoxExpectedQuantity > 0);
            }
            else
            {
                Slots[i].Set(string.Empty, 0, 0);
            }
        }
    }


    public async Task AddProductAsync(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        var value = barcode.Trim();

        try
        {
            if (IsWaitingForBoxScan || string.IsNullOrWhiteSpace(CurrentBoxId))
            {
                SetCurrentBox(value);
                IsWaitingForBoxScan = false;
                CurrentBarcode = string.Empty;
                LastScannedBarcode = $"Box: {value}";
                RefreshBoxHint();
                await LoadRecentAsync();
                return;
            }

            _scanningService.SetMode(InventoryMode.Loots, CurrentBoxId);
            _scanningService.Enqueue(value);
            CurrentBarcode = value;
            LastScannedBarcode = value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Loot scan failed: {ex}");
        }
    }


    private string GetCurrentBoxPreferenceKey()
    {
        var context = SessionHelper.GetCurrentScanContext(InventoryMode.Loots);

        var role = context.Role ?? "worker";

        return $"CurrentLootBoxId_{context.ServerKey}_{context.Module}_{context.DocumentId}_{role}";
    }

    private void SetCurrentBox(string? boxId)
    {
        var normalized = string.IsNullOrWhiteSpace(boxId) ? string.Empty : boxId.Trim();
        CurrentBoxId = normalized;
        CurrentBoxDisplay = string.IsNullOrWhiteSpace(normalized) ? "No box selected" : normalized;

        if (string.IsNullOrWhiteSpace(normalized))
            Preferences.Remove(GetCurrentBoxPreferenceKey());
        else
            Preferences.Set(GetCurrentBoxPreferenceKey(), normalized);

        _scanningService.SetMode(InventoryMode.Loots, string.IsNullOrWhiteSpace(normalized) ? null : normalized);
        RefreshBoxHint();
    }

    private void EnableBoxScanMode()
    {
        IsWaitingForBoxScan = true;
        CurrentBarcode = string.Empty;
        BoxScanHint = "Next scan will be used as the Box ID.";
    }

    private async Task SetBoxManuallyAsync()
    {
        var boxId = await Shell.Current.DisplayPromptAsync(
            "Set Box",
            "Enter or scan Box ID:",
            "Set",
            "Cancel",
            initialValue: CurrentBoxId);

        if (string.IsNullOrWhiteSpace(boxId))
            return;

        SetCurrentBox(boxId);
        IsWaitingForBoxScan = false;
        await LoadRecentAsync();
    }

    private void ClearCurrentBox()
    {
        SetCurrentBox(string.Empty);
        IsWaitingForBoxScan = true;
        ClearSlots();
    }

    private void ClearSlots()
    {
        foreach (var slot in Slots)
            slot.Set(string.Empty, 0, 0);
    }

    private void RefreshBoxHint()
    {
        BoxScanHint = string.IsNullOrWhiteSpace(CurrentBoxId) || IsWaitingForBoxScan
            ? "Scan a box ID first. Product scans start after a box is selected."
            : "Product scans will be saved under this box. Tap Scan Box before the next box.";
    }


    //    public async Task ExportDataAsync()
    //    {
    //        try
    //        {
    //            await _popup.ShowProgressAsync("Exporting Loots data...");
    //#if ANDROID
    //            var path = Android.OS.Environment.GetExternalStoragePublicDirectory(
    //                Android.OS.Environment.DirectoryDownloads).AbsolutePath;
    //#else
    //            var path = FileSystem.AppDataDirectory;
    //#endif
    //            var file = Path.Combine(path, $"Loots_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    //            var progress = new Progress<double>(p => _popup.UpdateMessage($"Progress {p:P0}"));
    //            await _exporter.ExportProductsAsync(file, progress, InventoryMode.Loots);
    //            _popup?.Close();
    //            if (_dialogs != null)
    //                await _dialogs.ShowMessageAsync("✅ Export Complete", $"Saved to: {file}");
    //            else
    //                Console.WriteLine($"[INFO] Export Complete (no dialog service). Saved to: {file}");
    //        }
    //        catch (Exception ex)
    //        {
    //            _popup?.Close();
    //            _logger.Error("Loots export failed", ex);
    //            if (_dialogs != null)
    //                await _dialogs.ShowMessageAsync("❌ Export Error", ex.Message);
    //            else
    //                Console.WriteLine($"[ERROR] Export Error: {ex}");
    //        }
    //    }

    private async Task ShowResultsAsync()
    {
        await _popup.ShowProgressAsync("Calculating loots totals...");
        var (initial, scanned, total, scannedCount) = await _productService.GetInventoryStats(InventoryMode.Loots);
        _popup.Close();

        string msg = $"📦 Loots Summary\n\n" +
                     $"Scanned: {scanned:N0}\n" +
                     $"Expected: {initial:N0}\n" +
                     $"Difference: {scanned - initial:N0}\n\n" +
                     $"Barcodes: {scannedCount}/{total}";
        await _dialogs.ShowMessageAsync("Loots Results", msg);
    }

    private async Task OnSlotTappedAsync(ProductSlot slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.Barcode))
            return;

        var product = await _productService.GetByBarcodeAsync(slot.Barcode, InventoryMode.Loots, CurrentBoxId);
        if (product == null)
            return;

        var query = new Dictionary<string, object>
        {
            ["Barcode"] = product.Barcode,
            ["Quantity"] = product.ScannedQuantity,
            ["InitialQuantity"] = product.InitialQuantity,
            ["Name"] = product.Name ?? "",
            ["Color"] = product.Color ?? "",
            ["Size"] = product.Size ?? "",
            ["Price"] = decimal.TryParse(product.Price, out var p) ? p : 0,
            ["ArticCode"] = product.ArticCode ?? "",
            ["IsReadOnly"] = false
        };

        await Shell.Current.GoToAsync(nameof(DetailsPage), new Dictionary<string, object>
        {
            ["Barcode"] = product.Barcode,
            ["IsReadOnly"] = false,
            ["Mode"] = InventoryMode.Loots
        });

    }
    // Example: when navigating to Settings from LootsScanningPage
    // Example command in LootsScanningViewModel
    // In LootsScanningViewModel.cs
    [RelayCommand]
    private async Task GoToSettings()
    {
        var vm = new SettingsViewModel();
        vm.SetLootsMode(true); // ← show the buttons

        await Shell.Current.GoToAsync(nameof(SettingsPage), true, new Dictionary<string, object>
        {
            ["BindingContext"] = vm // pass VM so x:DataType works
        });
    }
    public void Dispose()
    {
        _scanningService.Stop();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}