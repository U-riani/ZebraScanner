using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Helpers;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.Views;
#if ANDROID
using Android.Media;
#endif

namespace ZebraSCannerTest1.UI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{

    private readonly IProductRepository _products;
    private readonly IScanLogRepository _logs;
    private readonly IDataImportService _importer;
    private readonly IExcelExportService _exporter;
    private readonly IExcelExportLogsService _logExporter;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ILoggerService<MainViewModel> _logger;
    private readonly ZebraSCannerTest1.UI.Services.PopupService _popup;
    private readonly IScanningService _scanningService;


    private string _currentSection = Preferences.Get("CurrentSection", string.Empty);
    private readonly BlockingCollection<string> _scanQueue = new();
    private readonly CancellationTokenSource _scanCts = new();


    public const int SlotCount = 8;

    [ObservableProperty] private string currentBarcode = string.Empty;
    [ObservableProperty] private string importStatusText = string.Empty;
    [ObservableProperty] private bool isManualEntryVisible = true;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isNavigating;

    public ObservableCollection<ProductSlot> Slots { get; } =
        new(Enumerable.Range(0, SlotCount).Select(_ => new ProductSlot()));

    public IAsyncRelayCommand<string> AddProductCommand { get; }
    public IAsyncRelayCommand ImportDataCommand { get; }
    public IAsyncRelayCommand ExportDataCommand { get; }
    public IAsyncRelayCommand ShowResultsCommand { get; }
    public IAsyncRelayCommand ChangeSectionCommand { get; }
    public IAsyncRelayCommand GoToLogsCommand { get; }
    public IAsyncRelayCommand GoToScannedProductsCommand { get; }
    public IAsyncRelayCommand<ProductSlot> GoToDetailsCommand { get; }

    public MainViewModel(
        IProductRepository products,
        IScanLogRepository logs,
        IDataImportService importer,
        IExcelExportService exporter,
        IExcelExportLogsService logExporter,
        IDialogService dialogs,
        INavigationService navigation,
        ILoggerService<MainViewModel> logger,
        ZebraSCannerTest1.UI.Services.PopupService popup,
        IScanningService scanningService)
    {
        _products = products;
        _logs = logs;
        _importer = importer;
        _exporter = exporter;
        _logExporter = logExporter;
        _dialogs = dialogs;
        _navigation = navigation;
        _logger = logger;
        _popup = popup;
        _scanningService = scanningService;

        Task.Run(ProcessScanQueueAsync);


        AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
        ImportDataCommand = new AsyncRelayCommand(OnImportDataAsync);
        ExportDataCommand = new AsyncRelayCommand(ExportDataAsync);
        ChangeSectionCommand = new AsyncRelayCommand(ChangeSectionAsync);

        ShowResultsCommand = new AsyncRelayCommand(ShowResultsAsync);


        GoToLogsCommand = new AsyncRelayCommand(() =>
            NavigateSafelyAsync(() => _navigation.NavigateToAsync(nameof(LogsPage)))); GoToScannedProductsCommand = new AsyncRelayCommand(() => _navigation.NavigateToAsync(nameof(ScannedProductsPage)));
        
        GoToScannedProductsCommand = new AsyncRelayCommand(() =>
            NavigateSafelyAsync(() => _navigation.NavigateToAsync(nameof(ScannedProductsPage))));

        GoToDetailsCommand = new AsyncRelayCommand<ProductSlot>(OnSlotTappedAsync);

        _ = LoadRecentAsync();
        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, async (r, msg) =>
        {
            await LoadRecentAsync();
        });
    }

    // === Loaders ===
    private async Task LoadRecentAsync()
    {
        try
        {
            var recent = await _products.GetRecentAsync(SlotCount);
            int i = 0;
            foreach (var p in recent)
            {
                if (i < Slots.Count)
                {
                    Slots[i].Set(p.Barcode, p.ScannedQuantity, p.InitialQuantity);
                    i++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load recent products", ex);
        }
    }

    private async Task AddProductAsync(string? scannedBarcode)
    {
        if (string.IsNullOrWhiteSpace(scannedBarcode)) return;

        _scanQueue.Add(scannedBarcode.Trim());
        CurrentBarcode = scannedBarcode.Trim();
    }

    // === Scanning ===
    private async Task ProcessScanQueueAsync()
    {
        await Task.Run(async () =>
        {
            foreach (var barcode in _scanQueue.GetConsumingEnumerable(_scanCts.Token))
            {
                await _scanningService.ProcessAsync(barcode);
                await LoadRecentAsync();
            }
        });
    }


    // === Import with Progress Popup ===
    private async Task OnImportDataAsync()
    {
        bool popupOpened = false;
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select File to Import",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "*/*" } }
                })
            });

            if (result == null)
                return;

            // Show popup AFTER user selects file
            await _popup.ShowProgressAsync("Preparing import...");
            popupOpened = true;

            var ext = Path.GetExtension(result.FileName).ToLowerInvariant();
            using var stream = await result.OpenReadAsync();

            await Task.Run(async () =>
            {
                switch (ext)
                {
                    case ".xlsx":
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            _popup.UpdateMessage("Importing Excel data..."));
                        await _importer.ImportExcelAsync(stream, result.FileName);
                        break;

                    case ".json":
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            _popup.UpdateMessage("Importing JSON data..."));
                        await _importer.ImportJsonAsync(stream);
                        break;

                    case ".db":
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            _popup.UpdateMessage("Importing database..."));
                        await _importer.ImportDbAsync(stream);
                        break;

                    default:
                        throw new InvalidOperationException("Please select a valid .xlsx, .json, or .db file.");
                }
            });

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                _popup.UpdateMessage("Finalizing...");
                await Task.Delay(250);
                _popup.Close();
            });

            popupOpened = false;
            await _dialogs.ShowMessageAsync("✅ Import Complete", $"File {result.FileName} imported successfully.");
            await LoadRecentAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Import failed", ex);
            if (popupOpened)
                await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
            await _dialogs.ShowMessageAsync("❌ Import Error", ex.Message);
        }
        finally
        {
            if (popupOpened)
                await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
        }
    }

    private async Task ExportDataAsync()
    {
        bool popupOpened = false;

        try
        {
            IsBusy = true;

            var choice = await Shell.Current.DisplayActionSheet(
                "Export Type",
                "Cancel", null,
                "Products", "Logs");

            if (choice == "Cancel" || string.IsNullOrWhiteSpace(choice))
                return;

            await _popup.ShowProgressAsync("Preparing export...");
            popupOpened = true;

#if ANDROID
        var path = Android.OS.Environment.GetExternalStoragePublicDirectory(
            Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#else
            var path = FileSystem.AppDataDirectory;
#endif

            var name = $"{choice}_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            var fullPath = Path.Combine(path, name);

            var progress = new Progress<double>(p =>
            {
                _popup.UpdateMessage($"Exporting... {(int)(p * 100)}%");
            });

            // Run export in background
            await Task.Run(async () =>
            {
                if (choice == "Products")
                    await _exporter.ExportProductsAsync(fullPath, progress);
                else
                    await _logExporter.ExportLogsAsync(fullPath, progress);
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _popup.UpdateMessage("Finishing up...");
            });

            await Task.Delay(200);
            _popup.Close();
            popupOpened = false;

            await _dialogs.ShowMessageAsync("✅ Export Complete", $"File saved: {name}");
        }
        catch (Exception ex)
        {
            _logger.Error("Export failed", ex);

            if (popupOpened)
                _popup.Close();

            await _dialogs.ShowMessageAsync("❌ Export Error", ex.Message);
        }
        finally
        {
            IsBusy = false;

            if (popupOpened)
                _popup.Close();
        }
    }

    private async Task OnSlotTappedAsync(ProductSlot slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.Barcode))
            return;

        var product = await _products.FindAsync(slot.Barcode);
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

        await Shell.Current.GoToAsync(nameof(DetailsPage), query);
    }

    private async Task ShowResultsAsync()
    {
        bool popupShown = false;
        try
        {
            await _popup.ShowProgressAsync("Calculating totals...");
            popupShown = true;

            var (totalInitial, totalScanned, totalBarcodes, scannedBarcodes) = _products.GetInventoryStats();

            _popup.Close();
            popupShown = false;

            int quantityDiff = totalScanned - totalInitial;
            int barcodeDiff = scannedBarcodes - totalBarcodes;

            string msg =
                "📊 Inventory Summary\n\n" +
                $"🔹 Quantities:\n" +
                $"• Scanned Total Qty: {totalScanned:N0}\n" +
                $"• Expected Total Qty: {totalInitial:N0}\n" +
                $"• Difference: {quantityDiff:N0}\n\n" +
                $"🔹 Barcodes:\n" +
                $"• Scanned Barcodes: {scannedBarcodes:N0}\n" +
                $"• Total Barcodes: {totalBarcodes:N0}\n" +
                $"• Difference: {barcodeDiff:N0}";

            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlert("📦 Inventory Results", msg, "OK"));
        }
        catch (Exception ex)
        {
            if (popupShown)
                _popup.Close();

            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlert("❌ Error", ex.Message, "OK"));
        }
    }

    private async Task ChangeSectionAsync()
    {
        var newSection = await Shell.Current.DisplayPromptAsync(
            "Set Section",
            "Enter section name:",
            "OK", "Cancel",
            initialValue: _currentSection);

        if (!string.IsNullOrWhiteSpace(newSection))
        {
            _currentSection = newSection.Trim();
            Preferences.Set("CurrentSection", _currentSection);
            await _dialogs.ShowMessageAsync("Section Updated", $"Now working in {_currentSection}");
        }
    }

    private async Task NavigateSafelyAsync(Func<Task> navigationAction)
    {
        if (isNavigating)
            return;

        isNavigating = true;
        IsBusy = true;

        try
        {
            await navigationAction();
        }
        finally
        {
            isNavigating = false;
            IsBusy = false;
            await LoadRecentAsync(); // refresh after coming back
        }
    }

    private async Task HandleErrorAsync(string context, Exception ex)
    {
        _logger.Error(context, ex);
        await _dialogs.ShowMessageAsync("❌ Error", ex.Message);
    }



    public void Dispose()
    {
        _scanCts.Cancel();
        _scanQueue.CompleteAdding();
        WeakReferenceMessenger.Default.UnregisterAll(this);

        GC.SuppressFinalize(this);
    }

}
