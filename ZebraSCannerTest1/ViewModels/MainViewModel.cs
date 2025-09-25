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
    private readonly LogBufferService _logBuffer;

    public event Action<Product> NewProductAdded;

    // === Scan queue ===
    private readonly Queue<string> _scanQueue = new();
    private readonly object _scanLock = new();
    private bool _isFlushingScans = false;

    // === Cache (all products) ===
    private readonly Dictionary<string, Product> _cache = new(); // Barcode -> Product

    // === UI state ===
    private string _currentBarcode;
    private string _showCurrentBarcode;
    private Product _selectedProduct;
    private bool _isNavigating;
    private bool _isManualEntryVisible = true;

    private readonly TimeSpan _uiFlushInterval = TimeSpan.FromMilliseconds(100);
    private readonly List<Product> _pendingUiInserts = new();
    private DateTime _lastUiFlush = DateTime.UtcNow;

    public ObservableCollection<Product> Products { get; } = new(); // UI-bound list

    // Prepared UPSERT
    private readonly SqliteCommand _upsertProductCmd;

    private const int MaxUiRows = 8; // show last N rows for performance

    public MainViewModel(SqliteConnection conn, ExcelImportService importService, LogBufferService logBuffer)
    {
        _conn = conn;
        _importService = importService;
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
        LoadProducts();

        AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
        GoToDetailsCommand = new AsyncRelayCommand<Product>(OnItemTappedAsync);
        GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
        ImportExcelCommand = new AsyncRelayCommand(OnImportExcelAsync);
        ToggleManualEntryCommand = new RelayCommand(() => IsManualEntryVisible = !IsManualEntryVisible);

        // Listen for product updates from other pages
        WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _cache[m.Product.Barcode] = m.Product;
                var row = Products.FirstOrDefault(p => p.Barcode == m.Product.Barcode);
                if (row != null)
                {
                    row.ScannedQuantity = m.Product.ScannedQuantity;
                    row.InitialQuantity = m.Product.InitialQuantity;
                    row.UpdatedAt = m.Product.UpdatedAt;
                }
            });
        });
    }

    // ==== Bindables ====
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

    public Product SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (_selectedProduct == value) return;
            _selectedProduct = value;
            OnPropertyChanged();
            // disable navigation for speed — enable if you need details page
        }
    }

    // ==== Commands ====
    public IAsyncRelayCommand ImportExcelCommand { get; }
    public ICommand AddProductCommand { get; }
    public ICommand GoToDetailsCommand { get; }
    public ICommand GoToLogsCommand { get; }
    public ICommand ToggleManualEntryCommand { get; }

    // ==== Cache ====
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

    public void LoadProducts()
    {
        Products.Clear();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt " +
                          $"FROM Products ORDER BY UpdatedAt DESC LIMIT {MaxUiRows}";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            Products.Add(new Product
            {
                Barcode = r.GetString(0),
                InitialQuantity = r.GetInt32(1),
                ScannedQuantity = r.GetInt32(2),
                CreatedAt = DateTime.Parse(r.GetString(3)),
                UpdatedAt = DateTime.Parse(r.GetString(4))
            });
        }
    }

    // ==== FAST scan path ====
    public async Task AddProductAsync(string scannedBarcode)
    {
        scannedBarcode = scannedBarcode?.Trim();
        if (string.IsNullOrEmpty(scannedBarcode)) return;

        lock (_scanLock) _scanQueue.Enqueue(scannedBarcode);

        ShowCurrentBarcode = scannedBarcode; // update instantly

        if (!_isFlushingScans)
            _ = Task.Run(ProcessScanQueueAsync);
    }


    //private async Task ProcessScanQueueAsync()
    //{
    //    _isFlushingScans = true;
    //    try
    //    {
    //        while (true)
    //        {
    //            string nextBarcode = null;
    //            lock (_scanLock)
    //            {
    //                if (_scanQueue.Count > 0)
    //                    nextBarcode = _scanQueue.Dequeue();
    //            }

    //            if (nextBarcode == null) break;

    //            if (!_cache.TryGetValue(nextBarcode, out var product))
    //            {
    //                product = new Product
    //                {
    //                    Barcode = nextBarcode,
    //                    InitialQuantity = 0,
    //                    ScannedQuantity = 0,
    //                    CreatedAt = DateTime.UtcNow,
    //                    UpdatedAt = DateTime.UtcNow
    //                };
    //                _cache[nextBarcode] = product;
    //            }

    //            product.ScannedQuantity++;
    //            product.UpdatedAt = DateTime.UtcNow;

    //            // Update UI only if visible (keep UI list light)
    //            await MainThread.InvokeOnMainThreadAsync(() =>
    //            {
    //                var existing = Products.FirstOrDefault(p => p.Barcode == nextBarcode);
    //                if (existing != null)
    //                {
    //                    existing.ScannedQuantity = product.ScannedQuantity;
    //                    existing.UpdatedAt = product.UpdatedAt;

    //                    // 🔥 Scroll to the existing product instead of always scrolling to the top
    //                    NewProductAdded?.Invoke(existing);
    //                }
    //                else
    //                {
    //                    if (Products.Count >= MaxUiRows)
    //                        Products.RemoveAt(Products.Count - 1);

    //                    var newProd = new Product
    //                    {
    //                        Barcode = product.Barcode,
    //                        InitialQuantity = product.InitialQuantity,
    //                        ScannedQuantity = product.ScannedQuantity,
    //                        CreatedAt = product.CreatedAt,
    //                        UpdatedAt = product.UpdatedAt
    //                    };
    //                    Products.Insert(0, newProd);

    //                    // ✅ Scroll only when adding a new product
    //                    NewProductAdded?.Invoke(newProd);
    //                }
    //            });



    //            // persist
    //            _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
    //            _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
    //            _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
    //            _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
    //            _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
    //            _upsertProductCmd.ExecuteNonQuery();

    //            // log
    //            var log = new ScanLog
    //            {
    //                Barcode = product.Barcode,
    //                ScannedQuantity = product.ScannedQuantity,
    //                Timestamp = DateTime.UtcNow
    //            };
    //            _logBuffer.AddLog(log);
    //            WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));
    //        }
    //    }
    //    finally
    //    {
    //        _isFlushingScans = false;
    //    }
    //}
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
                        InitialQuantity = 0,
                        ScannedQuantity = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _cache[nextBarcode] = product;
                }

                product.ScannedQuantity++;
                product.UpdatedAt = DateTime.UtcNow;

                // ✅ Update DB immediately
                _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
                _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
                _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
                _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
                _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
                _upsertProductCmd.ExecuteNonQuery();

                // ✅ Log immediately
                var log = new ScanLog
                {
                    Barcode = product.Barcode,
                    ScannedQuantity = product.ScannedQuantity,
                    Timestamp = DateTime.UtcNow
                };
                _logBuffer.AddLog(log);
                WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));

                // ✅ UI batch insert
                lock (_pendingUiInserts)
                {
                    var existing = Products.FirstOrDefault(p => p.Barcode == nextBarcode);
                    if (existing != null)
                    {
                        existing.ScannedQuantity = product.ScannedQuantity;
                        existing.UpdatedAt = product.UpdatedAt;
                    }
                    else
                    {
                        // Keep UI light (last 50 items only)
                        if (Products.Count >= MaxUiRows)
                            Products.RemoveAt(Products.Count - 1);

                        var newProd = new Product
                        {
                            Barcode = product.Barcode,
                            InitialQuantity = product.InitialQuantity,
                            ScannedQuantity = product.ScannedQuantity,
                            CreatedAt = product.CreatedAt,
                            UpdatedAt = product.UpdatedAt
                        };
                        _pendingUiInserts.Add(newProd);
                    }
                }

                // Flush UI if interval elapsed
                if ((DateTime.UtcNow - _lastUiFlush) > _uiFlushInterval)
                {
                    await FlushUiInsertsAsync();
                }
            }

            // Final flush
            await FlushUiInsertsAsync();
        }
        finally
        {
            _isFlushingScans = false;
        }
    }
    private async Task FlushUiInsertsAsync()
    {
        List<Product> batch;
        lock (_pendingUiInserts)
        {
            if (_pendingUiInserts.Count == 0) return;
            batch = new List<Product>(_pendingUiInserts);
            _pendingUiInserts.Clear();
            _lastUiFlush = DateTime.UtcNow;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var prod in batch)
            {
                Products.Insert(0, prod);
                NewProductAdded?.Invoke(prod); // keep scroll
            }
        });
    }

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
        LoadProducts();
    }

    private async Task OnItemTappedAsync(Product product) =>
        await Shell.Current.GoToAsync($"{nameof(DetailsPage)}?Barcode={product.Barcode}&Quantity={product.ScannedQuantity}&InitialQuantity={product.InitialQuantity}");

    private async Task OnGoToLogsAsync() =>
        await Shell.Current.GoToAsync(nameof(LogsPage));

    // ==== INotify ====
    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
