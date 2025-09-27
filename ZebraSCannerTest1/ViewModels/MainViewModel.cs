using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ZebraSCannerTest1.Helpers;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;
using ZebraSCannerTest1.Views;

namespace ZebraSCannerTest1.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly SqliteConnection _conn;
    private readonly ExcelImportService _importService;
    private readonly ExcelExportService _exportService;
    private readonly LogBufferService _logBuffer;

    // scan queue for thread-safe fast ingestion
    private readonly Queue<string> _scanQueue = new();
    private readonly object _scanLock = new();
    private bool _isFlushingScans = false;

    // cache for all products by barcode
    private readonly Dictionary<string, Product> _cache = new();

    // recent list that feeds the 8 static slots (most recent first)
    private readonly List<string> _recent = new(capacity: 8);

    // prepared UPSERT command
    private readonly SqliteCommand _upsertProductCmd;

    private string _currentBarcode;
    private string _showCurrentBarcode;
    private bool _isManualEntryVisible = true;

    public event PropertyChangedEventHandler PropertyChanged;
    public event Action<Product> NewProductAdded; // kept for compatibility, but not used for scrolling now

    public const int SlotCount = 8;

    // 8 static slots for the UI
    public ObservableCollection<ProductSlot> Slots { get; } = new(
        Enumerable.Range(0, SlotCount).Select(_ => new ProductSlot())
    );

    public IAsyncRelayCommand ExportExcelCommand { get; }


    public MainViewModel(SqliteConnection conn, ExcelImportService importService, ExcelExportService exportService, LogBufferService logBuffer)
    {
        _conn = conn;
        _importService = importService;
        _exportService = exportService;
        _logBuffer = logBuffer;

        // prepare UPSERT
        _upsertProductCmd = _conn.CreateCommand();
        _upsertProductCmd.CommandText = @"
INSERT INTO Products (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
VALUES ($barcode,$initial,$scanned,$created,$updated)
ON CONFLICT(Barcode) DO UPDATE SET
    ScannedQuantity = $scanned,
    UpdatedAt       = $updated;";
        _upsertProductCmd.Parameters.Add("$barcode", SqliteType.Text);
        _upsertProductCmd.Parameters.Add("$initial", SqliteType.Integer);
        _upsertProductCmd.Parameters.Add("$scanned", SqliteType.Integer);
        _upsertProductCmd.Parameters.Add("$created", SqliteType.Text);
        _upsertProductCmd.Parameters.Add("$updated", SqliteType.Text);

        LoadCache();
        LoadRecentIntoSlots();

        AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
        GoToDetailsCommand = new AsyncRelayCommand<Product>(OnItemTappedAsync);
        GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
        ImportExcelCommand = new AsyncRelayCommand(OnImportExcelAsync);
        ToggleManualEntryCommand = new RelayCommand(() => IsManualEntryVisible = !IsManualEntryVisible);

        // if details page updates something, refresh cache and slots
        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
        {
            _cache[m.Product.Barcode] = m.Product;
            // if that product is visible, refresh its slot
            var idx = _recent.IndexOf(m.Product.Barcode);
            if (idx >= 0 && idx < SlotCount)
            {
                UpdateSlotFromCache(idx, m.Product.Barcode);
            }
        });

        ExportExcelCommand = new AsyncRelayCommand(OnExportExcelAsync);

    }

    // =========== Bindables ===========
    public string CurrentBarcode
    {
        get => _currentBarcode;
        set { _currentBarcode = value; OnPropertyChanged(); }
    }

    public string ShowCurrentBarcode
    {
        get => _showCurrentBarcode;
        set { _showCurrentBarcode = value; OnPropertyChanged(); }
    }

    public bool IsManualEntryVisible
    {
        get => _isManualEntryVisible;
        set { _isManualEntryVisible = value; OnPropertyChanged(); }
    }

    // =========== Commands ===========
    public IAsyncRelayCommand ImportExcelCommand { get; }
    public ICommand AddProductCommand { get; }
    public ICommand GoToDetailsCommand { get; }
    public ICommand GoToLogsCommand { get; }
    public ICommand ToggleManualEntryCommand { get; }

    // =========== Data Loaders ===========
    private void LoadCache()
    {
        _cache.Clear();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM Products";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var p = new Product
            {
                Barcode = r.GetString(0),
                InitialQuantity = r.GetInt32(1),
                ScannedQuantity = r.GetInt32(2),
                CreatedAt = DateTime.Parse(r.GetString(3)),
                UpdatedAt = DateTime.Parse(r.GetString(4))
            };
            _cache[p.Barcode] = p;
        }
    }

    private void LoadRecentIntoSlots()
    {
        _recent.Clear();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT Barcode FROM Products ORDER BY UpdatedAt DESC LIMIT 8";
        using var r = cmd.ExecuteReader();
        while (r.Read()) _recent.Add(r.GetString(0));

        // populate the 8 static slots
        for (int i = 0; i < SlotCount; i++)
        {
            if (i < _recent.Count)
                UpdateSlotFromCache(i, _recent[i]);
            else
                ClearSlot(i);
        }
    }

    private void UpdateSlotFromCache(int slotIndex, string barcode)
    {
        if (!_cache.TryGetValue(barcode, out var p))
        {
            // if somehow not in cache, hydrate from DB quickly
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM Products WHERE Barcode=$b";
            cmd.Parameters.AddWithValue("$b", barcode);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                p = new Product
                {
                    Barcode = barcode,
                    InitialQuantity = r.GetInt32(0),
                    ScannedQuantity = r.GetInt32(1),
                    CreatedAt = DateTime.Parse(r.GetString(2)),
                    UpdatedAt = DateTime.Parse(r.GetString(3))
                };
                _cache[barcode] = p;
            }
            else
            {
                // nothing found; clear slot
                ClearSlot(slotIndex);
                return;
            }
        }

        var slot = Slots[slotIndex];
        // must marshal to UI thread for property-changed
        MainThread.BeginInvokeOnMainThread(() =>
        {
            slot.Barcode = p.Barcode;
            slot.InitialQuantity = p.InitialQuantity;
            slot.ScannedQuantity = p.ScannedQuantity;
        });
    }

    private void ClearSlot(int slotIndex)
    {
        var slot = Slots[slotIndex];
        MainThread.BeginInvokeOnMainThread(() =>
        {
            slot.Barcode = string.Empty;
            slot.InitialQuantity = 0;
            slot.ScannedQuantity = 0;
        });
    }

    // =========== Scanning ===========
    public async Task AddProductAsync(string scannedBarcode)
    {
        scannedBarcode = scannedBarcode?.Trim();
        if (string.IsNullOrEmpty(scannedBarcode)) return;

        lock (_scanLock) _scanQueue.Enqueue(scannedBarcode);

        ShowCurrentBarcode = scannedBarcode;

        if (!_isFlushingScans)
            _ = Task.Run(ProcessScanQueueAsync);
    }

    private async Task ProcessScanQueueAsync()
    {
        _isFlushingScans = true;
        try
        {
            while (true)
            {
                string nextBarcode = null;
                lock (_scanLock)
                {
                    if (_scanQueue.Count > 0)
                        nextBarcode = _scanQueue.Dequeue();
                }
                if (nextBarcode == null) break;

                if (!_cache.TryGetValue(nextBarcode, out var product))
                {
                    product = new Product
                    {
                        Barcode = nextBarcode,
                        InitialQuantity = 0, // until Excel says otherwise
                        ScannedQuantity = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _cache[nextBarcode] = product;
                }

                product.ScannedQuantity++;
                product.UpdatedAt = DateTime.UtcNow;

                // persist to DB immediately
                _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
                _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
                _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
                _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
                _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
                _upsertProductCmd.ExecuteNonQuery();

                // log, buffered
                var log = new ScanLog
                {
                    Barcode = product.Barcode,
                    ScannedQuantity = product.ScannedQuantity,
                    Timestamp = DateTime.UtcNow
                };
                _logBuffer.AddLog(log);
                WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));

                // update the recent list: dedupe then insert at front
                int existing = _recent.IndexOf(nextBarcode);
                if (existing >= 0) _recent.RemoveAt(existing);
                _recent.Insert(0, nextBarcode);
                if (_recent.Count > SlotCount) _recent.RemoveAt(_recent.Count - 1);

                // repaint the 8 slots without moving UI elements
                for (int i = 0; i < SlotCount; i++)
                {
                    if (i < _recent.Count)
                        UpdateSlotFromCache(i, _recent[i]);
                    else
                        ClearSlot(i);
                }
            }
        }
        finally
        {
            _isFlushingScans = false;
        }
    }

    // =========== Navigation & Import ===========
    private async Task OnImportExcelAsync()
    {
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Select Excel File",
            FileTypes = FileTypes.Excel
        });
        if (result == null) return;

        await _importService.ImportExcelAsync(result.FullPath);
        LoadCache();
        LoadRecentIntoSlots();
    }

    private async Task OnItemTappedAsync(Product product)
    {
        if (product == null) return;
        await Shell.Current.GoToAsync($"{nameof(DetailsPage)}?Barcode={product.Barcode}&Quantity={product.ScannedQuantity}&InitialQuantity={product.InitialQuantity}");
    }


    private async Task OnExportExcelAsync()
    {
#if ANDROID
        // Save into Downloads with timestamped filename
        var downloadsPath = Android.OS.Environment
            .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)
            .AbsolutePath;

        var fileName = $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        var exportPath = Path.Combine(downloadsPath, fileName);
#else
    // Fallback for other platforms
    var fileName = $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
    var exportPath = Path.Combine(FileSystem.AppDataDirectory, fileName);
#endif

        await _exportService.ExportProductsAsync(exportPath);

        Console.WriteLine($"[DOTNET] ✅ Export complete. File saved at {exportPath}");

        await Shell.Current.DisplayAlert(
            "Export Complete",
            $"File saved in Downloads:\n{fileName}",
            "OK"
        );
    }


    private async Task OnGoToLogsAsync() =>
        await Shell.Current.GoToAsync(nameof(LogsPage));

    // =========== INotify ===========
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
