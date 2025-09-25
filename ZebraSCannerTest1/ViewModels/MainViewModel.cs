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

    // Scan buffer
    private readonly Queue<string> _scanQueue = new();
    private readonly object _scanLock = new();
    private bool _isFlushingScans = false;

    // Cache
    private readonly Dictionary<string, Product> _cache = new(); // Barcode -> Product

    // UI state
    private string _currentBarcode;
    private string _showCurrentBarcode;
    private Product _selectedProduct;
    private bool _isNavigating;
    private bool _isManualEntryVisible = true;

    public ObservableCollection<Product> Products { get; } = new();

    // Prepared UPSERT command
    private readonly SqliteCommand _upsertProductCmd;

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

        // Listen for detail page updates
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
                    Products.Move(Products.IndexOf(row), 0);
                }
                else
                {
                    Products.Add(m.Product);
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

            //    if (_selectedProduct != null && !_isNavigating)
            //    {
            //        _ = NavigateToDetailsAsync(_selectedProduct);
            //        _selectedProduct = null;
            //        OnPropertyChanged(nameof(SelectedProduct));
            //    }
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
        cmd.CommandText = "SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM Products ORDER BY UpdatedAt DESC LIMIT 10";
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

        // enqueue scan, process later
        lock (_scanLock) _scanQueue.Enqueue(scannedBarcode);

        // show user last scanned code instantly
        ShowCurrentBarcode = scannedBarcode;

        // start background worker if not running
        if (!_isFlushingScans)
            _ = Task.Run(ProcessScanQueueAsync);
    }

    // ==== Background worker ====
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

                if (nextBarcode == null) break; // nothing left

                // === Process scan ===
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

                // update UI (on main thread)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var existing = Products.FirstOrDefault(p => p.Barcode == nextBarcode);
                    if (existing != null)
                    {
                        existing.ScannedQuantity = product.ScannedQuantity;
                        existing.UpdatedAt = product.UpdatedAt;
                        Products.Move(Products.IndexOf(existing), 0);
                    }
                    else
                    {
                        Products.Insert(0, new Product
                        {
                            Barcode = product.Barcode,
                            InitialQuantity = product.InitialQuantity,
                            ScannedQuantity = product.ScannedQuantity,
                            CreatedAt = product.CreatedAt,
                            UpdatedAt = product.UpdatedAt
                        });
                    }
                });

                // persist to DB (UPSERT)
                _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
                _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
                _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
                _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
                _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
                _upsertProductCmd.ExecuteNonQuery();

                // log
                var log = new ScanLog
                {
                    Barcode = product.Barcode,
                    ScannedQuantity = product.ScannedQuantity,
                    Timestamp = DateTime.UtcNow
                };
                _logBuffer.AddLog(log);
                WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));
            }
        }
        finally
        {
            _isFlushingScans = false;
        }
    }

    // ==== Background queue flusher ====
    private async Task FlushScanQueueAsync()
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
                    else
                        break;
                }

                if (nextBarcode != null && _cache.TryGetValue(nextBarcode, out var product))
                {
                    // Update DB
                    _upsertProductCmd.Parameters["$barcode"].Value = product.Barcode;
                    _upsertProductCmd.Parameters["$initial"].Value = product.InitialQuantity;
                    _upsertProductCmd.Parameters["$scanned"].Value = product.ScannedQuantity;
                    _upsertProductCmd.Parameters["$created"].Value = product.CreatedAt.ToString("o");
                    _upsertProductCmd.Parameters["$updated"].Value = product.UpdatedAt.ToString("o");
                    _upsertProductCmd.ExecuteNonQuery();

                    // Save log
                    var log = new ScanLog
                    {
                        Barcode = product.Barcode,
                        ScannedQuantity = product.ScannedQuantity,
                        Timestamp = DateTime.UtcNow
                    };
                    _logBuffer.AddLog(log);

                    // Broadcast
                    WeakReferenceMessenger.Default.Send(new NewScanLogMessage(log));
                }
            }
        }
        finally
        {
            _isFlushingScans = false;
        }
    }

    // ==== Import / Navigation ====
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

    private async Task NavigateToDetailsAsync(Product product)
    {
        if (product == null) return;
        _isNavigating = true;
        try
        {
            await Shell.Current.GoToAsync($"{nameof(DetailsPage)}?Barcode={product.Barcode}&Quantity={product.ScannedQuantity}&InitialQuantity={product.InitialQuantity}");
        }
        finally
        {
            _isNavigating = false;
            SelectedProduct = null;
        }
    }

    private async Task OnItemTappedAsync(Product product) => await NavigateToDetailsAsync(product);

    private async Task OnGoToLogsAsync() => await Shell.Current.GoToAsync(nameof(LogsPage));

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
