using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels;
public class InventorizationByLootsMenuViewModel : ObservableObject
{
    public IRelayCommand NavigateToContinueCommand { get; }
    public IRelayCommand NavigateToResultCommand { get; }
    public IRelayCommand ExportLootsCommand { get; }
    public IRelayCommand ImportLootsCommand { get; }
    public IRelayCommand ClearResultCommand { get; }

    private readonly ILootsProductRepository _repo;

    private readonly IDataImportService _importer;
    private readonly IExcelExportService _exporter;
    private readonly IDialogService _dialogs;
    private readonly ILoggerService<InventorizationByLootsMenuViewModel> _logger;
    private readonly PopupService _popup;
    private readonly IProductService _productService;

    public InventorizationByLootsMenuViewModel(
        IDataImportService importer,
        IExcelExportService exporter,
        IDialogService dialogs,
        ILoggerService<InventorizationByLootsMenuViewModel> logger,
        PopupService popup,
        IProductService productService)
    {
        _importer = importer;
        _exporter = exporter;
        _dialogs = dialogs;
        _logger = logger;
        _popup = popup;
        _productService = productService;

        NavigateToContinueCommand = new AsyncRelayCommand(OnContinueAsync);
        NavigateToResultCommand = new AsyncRelayCommand(OnResultAsync);
        ExportLootsCommand = new AsyncRelayCommand(OnExportAsync);
        ImportLootsCommand = new AsyncRelayCommand(OnImportAsync);
        ClearResultCommand = new AsyncRelayCommand(OnClearAsync);
    }

    private async Task OnContinueAsync()
    {
        await Shell.Current.GoToAsync(nameof(InventorizationByLootsPage));
    }

    private async Task OnResultAsync()
    {
        try
        {
            await _popup.ShowProgressAsync("Calculating results...");
            var (totalInitial, totalScanned, totalBarcodes, scannedBarcodes) =
                _productService.GetInventoryStats(InventoryMode.Loots);

            _popup.Close();

            string message =
                $"📦 LOOTS INVENTORY SUMMARY\n\n" +
                $"🔹 Quantities:\n" +
                $"• Expected: {totalInitial:N0}\n" +
                $"• Scanned: {totalScanned:N0}\n" +
                $"• Difference: {totalScanned - totalInitial:N0}\n\n" +
                $"🔹 Barcodes:\n" +
                $"• Total: {totalBarcodes:N0}\n" +
                $"• Scanned: {scannedBarcodes:N0}\n" +
                $"• Missing: {totalBarcodes - scannedBarcodes:N0}";

            await Shell.Current.DisplayAlert("Loots Results", message, "OK");
        }
        catch (Exception ex)
        {
            _popup.Close();
            _logger.Error("Failed to show loots results", ex);
            await _dialogs.ShowMessageAsync("Error", ex.Message);
        }
    }

    private async Task OnExportAsync()
    {
        bool popupOpened = false;
        try
        {
            await _popup.ShowProgressAsync("Preparing export...");
            popupOpened = true;

#if ANDROID
            var path = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#else
            var path = FileSystem.AppDataDirectory;
#endif
            var file = Path.Combine(path, $"Loots_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");

            var progress = new Progress<double>(p => _popup.UpdateMessage($"Exporting... {(int)(p * 100)}%"));
            await _exporter.ExportProductsAsync(file, progress); // reuse same service — it now works per mode if needed

            _popup.Close();
            popupOpened = false;

            await _dialogs.ShowMessageAsync("✅ Export Complete", $"File saved: {file}");
        }
        catch (Exception ex)
        {
            if (popupOpened) _popup.Close();
            _logger.Error("Loots export failed", ex);
            await _dialogs.ShowMessageAsync("❌ Export Error", ex.Message);
        }
    }

    private async Task OnImportAsync()
    {
        try
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Import Loots Data?",
                "This will overwrite existing Loots data. Continue?",
                "Yes", "Cancel");

            if (!confirm) return;

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select File to Import",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "*/*" } }
                })
            });

            if (result == null) return;

            await _popup.ShowProgressAsync("Importing Loots data...");
            using var stream = await result.OpenReadAsync();

            await _importer.ImportExcelAsync(stream, InventoryMode.Loots, result.FileName);

            _popup.Close();

            await Shell.Current.GoToAsync(nameof(InventorizationByLootsPage));

        }
        catch (Exception ex)
        {
            _popup.Close();
            _logger.Error("Loots import failed", ex);
            await _dialogs.ShowMessageAsync("❌ Import Error", ex.Message);
        }
    }

    private async Task OnClearAsync()
    {
        //try
        //{
        //    var confirm = await Shell.Current.DisplayAlert(
        //        "Clear Loots Data?",
        //        "This will permanently delete all Loots products and logs.",
        //        "Yes", "Cancel");

        //    if (!confirm) return;

        //    await _popup.ShowProgressAsync("Clearing data...");

        //    using var cmd = _productService switch
        //    {
        //        // clear LootsProducts directly if service isn’t abstracted
        //        _ => _productService // just for clarity; you'd ideally have a ClearLootsAsync in repository
        //    };

        //    var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" +
        //        Path.Combine(FileSystem.AppDataDirectory, "zebraScannerData.db"));
        //    conn.Open();
        //    using (var clear = conn.CreateCommand())
        //    {
        //        clear.CommandText = "DELETE FROM LootsProducts; DELETE FROM LootsScanLogs;";
        //        clear.ExecuteNonQuery();
        //    }
        //    conn.Close();

        //    _popup.Close();
        //    await _dialogs.ShowMessageAsync("✅ Cleared", "All loots data removed.");
        //}
        //catch (Exception ex)
        //{
        //    _popup.Close();
        //    _logger.Error("Failed to clear Loots data", ex);
        //    await _dialogs.ShowMessageAsync("❌ Clear Error", ex.Message);
        //}
    }
}