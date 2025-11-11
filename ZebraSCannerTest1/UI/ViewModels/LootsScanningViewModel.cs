using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Messages;
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
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<ProductSlot> Slots { get; } =
        new(Enumerable.Range(0, 8).Select(_ => new ProductSlot()));

    public IAsyncRelayCommand<string> AddProductCommand { get; }
    //public IAsyncRelayCommand ExportDataCommand { get; }
    public IAsyncRelayCommand ShowResultsCommand { get; }
    public IAsyncRelayCommand GoToLogsCommand { get; }
    public IAsyncRelayCommand GoToScannedProductsCommand { get; }
    public IAsyncRelayCommand GoToDetailsCommand { get; }

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
            if (slot == null || string.IsNullOrWhiteSpace(slot.Barcode))
                return;

            // Try load product from DB first
            var product = await _productService.GetByBarcodeAsync(slot.Barcode, InventoryMode.Loots, currentBoxId);
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

        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(
            this, async (_, _) => await LoadRecentAsync());
    }


    public async Task InitializeAsync()
    {
        WeakReferenceMessenger.Default.Unregister<ProductUpdatedMessage>(this);
        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(
            this, async (_, _) => await LoadRecentAsync());


        if (string.IsNullOrWhiteSpace(CurrentBoxId))
        {
            // Try to re-fetch it from Preferences or scanning service
            CurrentBoxId = Preferences.Get("CurrentBoxId", string.Empty);
        }

        _scanningService.SetMode(InventoryMode.Loots, CurrentBoxId);
        _scanningService.StartAsync();

        await LoadRecentAsync();
    }


    public async Task LoadRecentAsync()
    {
        //if (string.IsNullOrWhiteSpace(CurrentBoxId))
        //{
        //    //await _dialogs.ShowMessageAsync("No Box Selected", "Please choose a loot box first.");
        //    return;
        //}

        var products = await _productService.GetProductsByBoxAsync(CurrentBoxId, InventoryMode.Loots);

        for (int i = 0; i < Slots.Count; i++)
        {
            if (i < products.Count())
            {
                var p = products.ElementAt(i);
                Slots[i].Set(p.Barcode, p.ScannedQuantity, p.InitialQuantity);
            }
            else
            {
                Slots[i].Set(string.Empty, 0, 0);
            }
        }
    }


    public Task AddProductAsync(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return Task.CompletedTask;

        try
        {
            if (string.IsNullOrWhiteSpace(CurrentBoxId))
            {
                Console.WriteLine("[WARN] BoxId missing during scan. Ignoring scan.");
                return Task.CompletedTask;
            }

            _scanningService.Enqueue(barcode.Trim());
            CurrentBarcode = barcode.Trim();
            LastScannedBarcode = barcode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Loot scan failed: {ex}");
        }

        return Task.CompletedTask;
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
        var (initial, scanned, total, scannedCount) = _productService.GetInventoryStats(InventoryMode.Loots);
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

        var product = await _productService.GetByBarcodeAsync(slot.Barcode);
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
    public void Dispose()
    {
        _scanningService.Stop();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}