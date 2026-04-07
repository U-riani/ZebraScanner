using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels;

[QueryProperty(nameof(Mode), "Mode")]
[QueryProperty(nameof(DocumentId), "DocumentId")]
[QueryProperty(nameof(ServerDbModule), "ServerDbModule")]
[QueryProperty(nameof(DocumentStatus), "DocumentStatus")]
public partial class InventorizationMenuViewModel : ObservableObject
{
    [ObservableProperty]
    private InventoryMode mode;

    [ObservableProperty]
    private int documentId;

    [ObservableProperty]
    private string serverDbModule;

    [ObservableProperty]
    private string documentStatus;

    public bool ShowLoadDataButton =>
    DocumentStatus?.Equals("waiting_to_start", StringComparison.OrdinalIgnoreCase) == true
    || DocumentStatus?.Contains("recount_requested", StringComparison.OrdinalIgnoreCase) == true
    || DocumentStatus?.Equals("sender_recount_completed", StringComparison.OrdinalIgnoreCase) == true;

    public bool ShowFinishScanningButton =>
    DocumentStatus?.Equals("in_progress", StringComparison.OrdinalIgnoreCase) == true
    || DocumentStatus?.Equals("sender_in_progress", StringComparison.OrdinalIgnoreCase) == true
    || DocumentStatus?.Equals("sender_recount_in_progress", StringComparison.OrdinalIgnoreCase) == true
    || DocumentStatus?.Equals("recount_in_progress", StringComparison.OrdinalIgnoreCase) == true
    || DocumentStatus?.Equals("receive_recount_in_progress", StringComparison.OrdinalIgnoreCase) == true;

    partial void OnDocumentStatusChanged(string value)
    {
        OnPropertyChanged(nameof(ShowLoadDataButton));
        OnPropertyChanged(nameof(ShowFinishScanningButton));
    }

    public IRelayCommand LoadDataFromServerCommand { get; }
    public IRelayCommand NavigateToContinueCommand { get; }
    public IRelayCommand NavigateToResultCommand { get; }
    public IRelayCommand ExportCommand { get; }
    public IRelayCommand ImportCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand TestDocLinesCommand { get; }
    public IRelayCommand FinishScanningCommand { get; }

    private readonly IDataImportService _importer;
    private readonly IExcelExportService _exporter;
    private readonly IExcelExportLogsService _logExporter;
    private readonly IDialogService _dialogs;
    private readonly ILoggerService<InventorizationMenuViewModel> _logger;
    private readonly PopupService _popup;
    private readonly IProductService _productService;
    private readonly ApiInventoryService _inventoryService = new();

    private readonly IJsonExportService _jsonExporter;
    private readonly IJsonExportLogsService _jsonLogExporter;
    private readonly IApiService _apiService;
    private readonly IServerImportService _serverImporter;
    private IScanLogRepository _scanLogRepository;

    public ObservableCollection<PocketDocumentLinesDto> DocumentLines { get; set; }
       = new();

    private bool _importLocked = false;

    public InventorizationMenuViewModel(
        IDataImportService importer,
        IExcelExportService exporter,
        IDialogService dialogs,
        ILoggerService<InventorizationMenuViewModel> logger,
        PopupService popup,
        IProductService productService,
        IExcelExportLogsService logExporter,
        IJsonExportService jsonExporter,
        IJsonExportLogsService jsonLogExporter,
        IApiService apiService,
        IServerImportService serverImporter,
        IScanLogRepository scanLogRepository)
    {
        _importer = importer;
        _exporter = exporter;
        _dialogs = dialogs;
        _logger = logger;
        _popup = popup;
        _productService = productService;
        _logExporter = logExporter;
        _jsonExporter = jsonExporter;
        _jsonLogExporter = jsonLogExporter;


        LoadDataFromServerCommand = new AsyncRelayCommand(OnLoadDataFromServerCommand);
        NavigateToContinueCommand = new AsyncRelayCommand(OnContinueAsync);
        NavigateToResultCommand = new AsyncRelayCommand(OnResultAsync);
        ExportCommand = new AsyncRelayCommand(OnExportAsync);
        ImportCommand = new AsyncRelayCommand(OnImportAsync);
        ClearCommand = new AsyncRelayCommand(OnClearAsync);
        TestDocLinesCommand = new AsyncRelayCommand(OnTestDocLinesCommand);
        FinishScanningCommand = new AsyncRelayCommand(OnFinishScanningCommand);

        _apiService = apiService;
        _serverImporter = serverImporter;
        _scanLogRepository = scanLogRepository;
    }

    //private string? GetNextStatusAfterLoad()
    //{
    //    var status = DocumentStatus?.Trim().ToLower();

    //    return status switch
    //    {
    //        "waiting_to_start" => "in_progress",
    //        "recount_requested" => "recount_in_progress",
    //        "sender_recount_requested" => "sender_recount_in_progress",
    //        "receive_recount_requested" => "receive_recount_in_progress",
    //        _ => null
    //    };
    //}
    private string? ResolveTransferRole()
    {
        if (!string.Equals(ServerDbModule, "transfer", StringComparison.OrdinalIgnoreCase))
            return null;

        var status = DocumentStatus?.Trim().ToLowerInvariant();

        if (status == "waiting_to_start" || status == "sender_recount_requested")
            return "sender";

        if (status == "sender_recount_completed" || status == "receive_recount_requested")
            return "receiver";

        return null;
    }


    private async Task<List<SubmitDocumentLineRowDto>> BuildSubmitRowsAsync()
    {
        var rows = new List<SubmitDocumentLineRowDto>();
        var products = await _productService.GetProductsForUploadAsync(Mode);

        foreach (var item in products)
        {
            if (item.ScannedQuantity <= 0)
                continue;

            rows.Add(new SubmitDocumentLineRowDto
            {
                line_id = null, // current local DB does not store backend line id
                barcode = item.Barcode,
                quantity = item.ScannedQuantity,
                box_id = Mode == InventoryMode.Loots ? item.Box_Id : null
            });
        }

        return rows;
    }

    private async Task OnLoadDataFromServerCommand()
    {
        bool popupOpened = false;

        try
        {
            var token = await SecureStorage.GetAsync("token");

            if (string.IsNullOrWhiteSpace(token))
            {
                await _dialogs.ShowMessageAsync("Error", "Token not found.");
                return;
            }

            await _popup.ShowProgressAsync("Loading document lines from server...");
            popupOpened = true;

            var docLines = await _inventoryService.GetDocumentLines(token, documentId, serverDbModule);

            if (docLines == null || !docLines.Any())
            {
                _popup.Close();
                popupOpened = false;
                await _dialogs.ShowMessageAsync("No Data", "No document lines were returned from server.");
                return;
            }

            DocumentLines.Clear();
            foreach (var line in docLines)
                DocumentLines.Add(line);

            _popup.UpdateMessage("Importing into local database...");

            int imported = await _importer.ImportBackendDocumentLinesAsync(docLines, Mode);

            string? role = null;

            if (string.Equals(serverDbModule, "transfer", StringComparison.OrdinalIgnoreCase))
            {
                var status = documentStatus?.Trim().ToLowerInvariant();

                if (status == "waiting_to_start" || status == "sender_recount_requested")
                {
                    role = "sender";
                }
                else if (status == "sender_recount_completed" || status == "receive_recount_requested")
                {
                    role = "receiver";
                }
            }

            _popup.UpdateMessage("Updating document status...");

            var updateResult = await _inventoryService.UpdateDocumentStatus(
                token,
                DocumentId,
                ServerDbModule,
                documentStatus,
                role);

            if (updateResult == null || !updateResult.ok)
            {
                _popup.Close();
                popupOpened = false;
                await _dialogs.ShowMessageAsync(
                    "Warning",
                    "Data loaded successfully, but failed to update document status on server.");
                return;
            }

            DocumentStatus = updateResult.assignment_status;



            await _scanLogRepository.ClearAsync(Mode);
            WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(new Product()));

            _popup.Close();
            popupOpened = false;

            await _dialogs.ShowMessageAsync(
                "Success",
                $"Loaded {docLines.Count} lines from server and imported {imported} rows into {Mode} database.");

        }
        catch (Exception ex)
        {
            _logger.Error("LoadDataFromServer failed", ex);

            if (popupOpened)
                _popup.Close();

            await _dialogs.ShowMessageAsync("Import Error", ex.Message);
        }
    }

    private async Task OnContinueAsync()
    {
        var targetPage = Mode == InventoryMode.Loots
            ? nameof(InventorizationByLootsPage)
            : nameof(InventorizationPage);

        await Shell.Current.GoToAsync(targetPage);
    }

    // === RESULT ===
    private async Task OnResultAsync()
    {
        try
        {
            Console.WriteLine("----DOcument is here amigo" + documentId);
            await _popup.ShowProgressAsync($"Calculating totals for {Mode}...");
            var (totalInitial, totalScanned, totalBarcodes, scannedBarcodes) =
                await _productService.GetInventoryStats(Mode);

            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());

            string modeName = Mode.ToString().ToUpper();
            int quantityDiff = totalScanned - totalInitial;
            int barcodeDiff = scannedBarcodes - totalBarcodes;

            string msg =
                $"📊 {modeName} INVENTORY SUMMARY\n\n" +
                $"🔹 Quantities:\n" +
                $"• Scanned Total Qty: {totalScanned:N0}\n" +
                $"• Expected Total Qty: {totalInitial:N0}\n" +
                $"• Difference: {quantityDiff:N0}\n\n" +
                $"🔹 Barcodes:\n" +
                $"• Scanned Barcodes: {scannedBarcodes:N0}\n" +
                $"• Total Barcodes: {totalBarcodes:N0}\n" +
                $"• Difference: {barcodeDiff:N0}";

            await Shell.Current.DisplayAlert("📦 Inventory Results", msg, "OK");
        }
        catch (Exception ex)
        {
            _popup.Close();
            _logger.Error($"{Mode} result calculation failed", ex);
            await _dialogs.ShowMessageAsync("❌ Error", ex.Message);
        }
    }

    // === EXPORT ===
    private async Task OnExportAsync()
    {
        bool popupOpened = false;
        try
        {
            var choice = await Shell.Current.DisplayActionSheet(
                $"Export {Mode} Data",
                "Cancel", null,
                "Products (Excel)", "Logs (Excel)", "Products (JSON)", "Logs (JSON)");

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

            string extension = choice.Contains("JSON") ? "json" : "xlsx";
            var fileName = $"{Mode}_{choice.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{extension}";
            var fullPath = Path.Combine(path, fileName);

            var progress = new Progress<double>(p =>
                _popup.UpdateMessage($"Exporting... {(int)(p * 100)}%"));

            // Decide which export service to use
            await Task.Run(async () =>
            {
                if (choice == "Products (Excel)")
                    await _exporter.ExportProductsAsync(fullPath, progress, Mode);
                else if (choice == "Logs (Excel)")
                    await _logExporter.ExportLogsAsync(fullPath, progress, Mode);
                else if (choice == "Products (JSON)")
                {
                    string json = await _jsonExporter.ExportProductsJsonAsync(null, progress, Mode);

                    try
                    {
                        var allProducts = JsonSerializer.Deserialize<List<object>>(json) ?? new();
                        const int batchSize = 5000;

                        for (int i = 0; i < allProducts.Count; i += batchSize)
                        {
                            var batch = allProducts.Skip(i).Take(batchSize).ToList();
                            var batchJson = JsonSerializer.Serialize(batch);

                            await _apiService.UploadInventoryJsonAsync(batchJson);

                            double percent = Math.Min((double)(i + batch.Count) / allProducts.Count, 1.0);
                            _popup.UpdateMessage($"Uploading... {(int)(percent * 100)}%");
                        }

                        Console.WriteLine($"✅ Uploaded {allProducts.Count} products in chunks.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Chunk upload failed: {ex.Message}");
                        throw;
                    }
                }

                else if (choice == "Logs (JSON)")
                {
                    string json = await _jsonLogExporter.ExportLogsJsonAsync(null, progress, Mode);
                    await _apiService.UploadInventoryJsonAsync(json);
                }
            });


            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _popup.UpdateMessage("Finalizing...");
                _popup.Close();
            });
            popupOpened = false;

            await _dialogs.ShowMessageAsync("✅ Export Complete", $"File saved: {fileName}");
        }
        catch (Exception ex)
        {
            if (popupOpened) _popup.Close();
            _logger.Error($"{Mode} export failed", ex);
            await _dialogs.ShowMessageAsync("❌ Export Error", ex.Message);
        }
    }


    // === IMPORT ===
    //private async Task OnImportAsync()
    //{
    //    var confirm = await Shell.Current.DisplayAlert(
    //        $"Import {Mode} Data?",
    //        $"This will overwrite existing {Mode} data. Continue?",
    //        "Yes", "Cancel");

    //    if (!confirm)
    //        return;

    //    if (_importLocked)
    //        return;

    //    _importLocked = true;
    //    bool popupOpened = false;

    //    try
    //    {
    //        var result = await FilePicker.PickAsync(new PickOptions
    //        {
    //            PickerTitle = $"Select File to Import for {Mode}",
    //            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    //            {
    //                { DevicePlatform.Android, new[] { "*/*" } },
    //                { DevicePlatform.WinUI, new[] { ".xlsx", ".json", ".db" } }
    //            })
    //        });

    //        if (result == null)
    //            return;

    //        await _popup.ShowProgressAsync("Preparing import...");
    //        popupOpened = true;

    //        string ext = Path.GetExtension(result.FileName).ToLowerInvariant();
    //        using var stream = await result.OpenReadAsync();

    //        await Task.Run(async () =>
    //        {
    //            switch (ext)
    //            {
    //                case ".xlsx":
    //                    await MainThread.InvokeOnMainThreadAsync(() =>
    //                        _popup.UpdateMessage("Importing Excel data..."));
    //                    await _importer.ImportExcelAsync(stream, Mode, result.FileName);
    //                    break;

    //                case ".json":
    //                    await MainThread.InvokeOnMainThreadAsync(() =>
    //                        _popup.UpdateMessage("Importing JSON data..."));
    //                    await _importer.ImportJsonAsync(stream, Mode);
    //                    break;

    //                case ".db":
    //                    await MainThread.InvokeOnMainThreadAsync(() =>
    //                        _popup.UpdateMessage("Importing database..."));
    //                    await _importer.ImportDbAsync(stream, Mode);
    //                    break;

    //                default:
    //                    throw new InvalidOperationException("Please select a valid .xlsx, .json, or .db file.");
    //            }
    //        });

    //        await MainThread.InvokeOnMainThreadAsync(() =>
    //        {
    //            _popup.UpdateMessage("Finalizing...");
    //            _popup.Close();
    //        });

    //        popupOpened = false;
    //        await _dialogs.ShowMessageAsync("✅ Import Complete", $"File {result.FileName} imported successfully.");
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.Error($"{Mode} import failed", ex);
    //        if (popupOpened)
    //            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
    //        await _dialogs.ShowMessageAsync("❌ Import Error", ex.Message);
    //    }
    //    finally
    //    {
    //        _importLocked = false;
    //        if (popupOpened)
    //            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
    //    }
    //}

    // === CLEAR ===
    //private async Task OnImportAsync()
    //{
    //    var choice = await Shell.Current.DisplayActionSheet(
    //        $"Import {Mode} Data",
    //        "Cancel", null,
    //        "From Device", "From Server");

    //    if (choice == "Cancel" || string.IsNullOrWhiteSpace(choice))
    //        return;

    //    await _popup.ShowProgressAsync("Preparing import...");
    //    bool popupOpened = true;

    //    try
    //    {
    //        if (choice == "From Server")
    //        {
    //            await _popup.ShowProgressAsync("Downloading from server...");
    //            int imported = await _serverImporter.ImportJsonFromServerAsync(Mode);
    //            _popup.UpdateMessage($"✅ Imported {imported} items from server.");
    //        }

    //        else // From Device
    //        {
    //            var result = await FilePicker.PickAsync(new PickOptions
    //            {
    //                PickerTitle = $"Select File to Import for {Mode}",
    //                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    //            {
    //                { DevicePlatform.Android, new[] { "*/*" } },
    //                { DevicePlatform.WinUI, new[] { ".xlsx", ".json", ".db" } }
    //            })
    //            });

    //            if (result == null)
    //                return;

    //            using var stream = await result.OpenReadAsync();
    //            string ext = Path.GetExtension(result.FileName).ToLowerInvariant();

    //            switch (ext)
    //            {
    //                case ".xlsx":
    //                    _popup.UpdateMessage("Importing Excel data...");
    //                    await _importer.ImportExcelAsync(stream, Mode, result.FileName);
    //                    break;
    //                case ".json":
    //                    _popup.UpdateMessage("Importing JSON data...");
    //                    await _importer.ImportJsonAsync(stream, Mode);
    //                    break;
    //                case ".db":
    //                    _popup.UpdateMessage("Importing database...");
    //                    await _importer.ImportDbAsync(stream, Mode);
    //                    break;
    //                default:
    //                    throw new InvalidOperationException("Please select a valid .xlsx, .json, or .db file.");
    //            }

    //            _popup.UpdateMessage("✅ Import Complete (Device)");
    //        }

    //        await MainThread.InvokeOnMainThreadAsync(() =>
    //        {
    //            _popup.Close();
    //        });
    //        popupOpened = false;

    //        await _dialogs.ShowMessageAsync("✅ Import Complete", $"Data imported successfully from {choice}");
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.Error($"{Mode} import failed", ex);
    //        if (popupOpened)
    //            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
    //        await _dialogs.ShowMessageAsync("❌ Import Error", ex.Message);
    //    }
    //    finally
    //    {
    //        if (popupOpened)
    //            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
    //    }
    //}
    private async Task OnImportAsync()
    {
        var choice = await Shell.Current.DisplayActionSheet(
            $"Import {Mode} Data",
            "Cancel", null,
            "From Device", "From Server");

        if (choice == "Cancel" || string.IsNullOrWhiteSpace(choice))
            return;

        bool popupOpened = false;

        try
        {
            await _popup.ShowProgressAsync("Preparing import...");
            popupOpened = true;

            int imported = 0;

            if (choice == "From Server")
            {
                _popup.UpdateMessage("Downloading from server...");
                imported = await _serverImporter.ImportJsonFromServerAsync(Mode);

                // Clear any cached scan logs
                await _scanLogRepository.ClearAsync(Mode);

                // 🔁 Notify UI layers
                WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(new Product()));

                _popup.UpdateMessage($"✅ Imported {imported} items from server.");
            }


            else // From Device
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = $"Select File to Import for {Mode}",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.WinUI, new[] { ".xlsx", ".json", ".db" } }
                })
                });

                if (result == null)
                    return;

                using var stream = await result.OpenReadAsync();
                string ext = Path.GetExtension(result.FileName).ToLowerInvariant();

                switch (ext)
                {
                    case ".xlsx":
                        _popup.UpdateMessage("Importing Excel data...");
                        await _importer.ImportExcelAsync(stream, Mode, result.FileName);
                        break;
                    case ".json":
                        _popup.UpdateMessage("Importing JSON data...");
                        await _importer.ImportJsonAsync(stream, Mode);
                        break;
                    case ".db":
                        _popup.UpdateMessage("Importing database...");
                        await _importer.ImportDbAsync(stream, Mode);
                        break;
                    default:
                        throw new InvalidOperationException("Please select a valid .xlsx, .json, or .db file.");
                }

                _popup.UpdateMessage("✅ Import Complete (Device)");
            }

            await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
            popupOpened = false;

            await _dialogs.ShowMessageAsync("✅ Import Complete", $"Data imported successfully from {choice}");
        }
        catch (Exception ex)
        {
            _logger.Error($"{Mode} import failed", ex);

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

    private async Task OnClearAsync()
    {
        await _dialogs.ShowMessageAsync("Not Implemented", "Clear data functionality not yet available.");
    }

    private async Task OnTestDocLinesCommand()
    {
        Console.WriteLine("++++++++++============");

        var token = await SecureStorage.GetAsync("token");

        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("TOKEN NOT FOUND");
            return;
        }

        var docLines = await _inventoryService.GetDocumentLines(token, documentId, serverDbModule);

        if (docLines == null)
            return;

        DocumentLines.Clear();

        Console.WriteLine("-------" + documentStatus);

        foreach (var line in docLines)
        {
            Console.WriteLine("++++++++ " + line.barcode); // or any field
            DocumentLines.Add(line);
        }

        Console.WriteLine($"Loaded {DocumentLines.Count} document lines.");
    }

    private async Task OnFinishScanningCommand()
    {
        bool popupOpened = false;

        try
        {
            var token = await SecureStorage.GetAsync("token");

            if (string.IsNullOrWhiteSpace(token))
            {
                await _dialogs.ShowMessageAsync("Error", "Token not found.");
                return;
            }

            var role = ResolveTransferRole();

            await _popup.ShowProgressAsync("Preparing scanned rows...");
            popupOpened = true;

            var rows = await BuildSubmitRowsAsync();

            if (rows == null || rows.Count == 0)
            {
                _popup.Close();
                popupOpened = false;
                await _dialogs.ShowMessageAsync("No Data", "No scanned rows found to upload.");
                return;
            }

            var payload = new SubmitDocumentLinesRequestDto
            {
                current_status = DocumentStatus,
                role = role,
                rows = rows
            };

            _popup.UpdateMessage("Uploading scanned rows to server...");

            var submitResult = await _inventoryService.SubmitDocumentLines(
                token,
                DocumentId,
                ServerDbModule,
                payload);

            if (submitResult == null || !submitResult.ok)
            {
                _popup.Close();
                popupOpened = false;
                await _dialogs.ShowMessageAsync("Upload Failed", "Could not upload scanned rows to server.");
                return;
            }

            _popup.UpdateMessage("Advancing document status...");

            var statusResult = await _inventoryService.FinishScanning(
                token,
                DocumentId,
                ServerDbModule,
                DocumentStatus,
                role);

            if (statusResult == null || !statusResult.ok)
            {
                _popup.Close();
                popupOpened = false;
                await _dialogs.ShowMessageAsync(
                    "Partial Success",
                    $"Uploaded {submitResult.updated_lines} rows, but failed to update status.");
                return;
            }

            DocumentStatus = statusResult.assignment_status;

            _popup.Close();
            popupOpened = false;

            await _dialogs.ShowMessageAsync(
                "Success",
                $"Uploaded {submitResult.updated_lines} rows and updated status to '{statusResult.assignment_status}'.");
        }
        catch (Exception ex)
        {
            _logger.Error("FinishScanning failed", ex);

            if (popupOpened)
                _popup.Close();

            await _dialogs.ShowMessageAsync("Error", ex.Message);
        }
    }
}


