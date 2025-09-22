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

namespace ZebraSCannerTest1.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SqliteConnection _conn;
        private readonly ExcelImportService _importService;

        // Caches
        private Dictionary<string, InitialProduct> _initialCache = new();
        private Dictionary<string, ScannedProduct> _scannedCache = new();

        // UI state
        private string _currentBarcode;
        private string _showCurrentBarcode;
        private ScannedProduct _selectedProduct;
        private bool _isNavigating;
        private bool _isShowingAlert;
        private bool _isManualEntryVisible = true;

        // Background save control
        private readonly object _saveLock = new();
        private bool _savePending;

        public ObservableCollection<ScannedProduct> Products { get; } = new();

        public MainViewModel(SqliteConnection conn, ExcelImportService importService)
        {
            _conn = conn;
            _importService = importService;

            LoadProducts();
            BuildInitialCache();
            BuildScannedCache();

            AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
            GoToDetailsCommand = new AsyncRelayCommand<ScannedProduct>(OnItemTappedAsync);
            GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
            ImportExcelCommand = new AsyncRelayCommand(OnImportExcelAsync);
            ToggleManualEntryCommand = new RelayCommand(() => IsManualEntryVisible = !IsManualEntryVisible);

            Task.Run(SaveLoopAsync);

            // 🔔 Listen for updates from DetailsPage
            WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _scannedCache[m.Product.Barcode] = m.Product;

                    var row = Products.FirstOrDefault(p => p.Barcode == m.Product.Barcode);
                    if (row != null)
                    {
                        row.Quantity = m.Product.Quantity;
                        row.InitialQuantity = m.Product.InitialQuantity;
                        row.UpdatedAt = m.Product.UpdatedAt;
                        Products.Move(Products.IndexOf(row), 0);
                    }
                    else
                    {
                        Products.Insert(0, m.Product);
                    }

                    lock (_saveLock) _savePending = true;
                });
            });
        }

        // ====== Bindable properties ======
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

        public ScannedProduct SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (_selectedProduct == value) return;
                _selectedProduct = value;
                OnPropertyChanged();

                if (_selectedProduct != null && !_isNavigating)
                {
                    _ = NavigateToDetailsAsync(_selectedProduct);
                    _selectedProduct = null;
                    OnPropertyChanged(nameof(SelectedProduct));
                }
            }
        }

        // ====== Commands ======
        public IAsyncRelayCommand ImportExcelCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand GoToDetailsCommand { get; }
        public ICommand GoToLogsCommand { get; }
        public ICommand ToggleManualEntryCommand { get; }

        // ====== Cache builders ======
        private void BuildInitialCache()
        {
            var dict = new Dictionary<string, InitialProduct>();

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Barcode, Quantity FROM InitialProducts";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dict[reader.GetString(1)] = new InitialProduct
                {
                    Id = reader.GetInt32(0),
                    Barcode = reader.GetString(1),
                    Quantity = reader.GetInt32(2)
                };
            }
            _initialCache = dict;

            Console.WriteLine($"[DOTNET] Initial cache size: {_initialCache.Count}");
        }

        private void BuildScannedCache()
        {
            var dict = new Dictionary<string, ScannedProduct>();

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Barcode, Quantity, InitialQuantity, CreatedAt, UpdatedAt FROM ScannedProducts";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dict[reader.GetString(1)] = new ScannedProduct
                {
                    Id = reader.GetInt32(0),
                    Barcode = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    InitialQuantity = reader.GetInt32(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    UpdatedAt = DateTime.Parse(reader.GetString(5))
                };
            }
            _scannedCache = dict;

            Console.WriteLine($"[DOTNET] Scanned cache size: {_scannedCache.Count}");
        }

        // ====== UI list load ======
        public void LoadProducts()
        {
            Products.Clear();

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Barcode, Quantity, InitialQuantity, CreatedAt, UpdatedAt FROM ScannedProducts ORDER BY UpdatedAt DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _initialCache.TryGetValue(reader.GetString(1), out var init);

                Products.Add(new ScannedProduct
                {
                    Id = reader.GetInt32(0),
                    Barcode = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    InitialQuantity = init?.Quantity ?? reader.GetInt32(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    UpdatedAt = DateTime.Parse(reader.GetString(5))
                });
            }
        }

        // ====== FAST scan path ======
        public async Task AddProductAsync(string scannedBarcode)
        {
            if (_isShowingAlert) return;

            scannedBarcode = scannedBarcode?.Trim();
            if (string.IsNullOrEmpty(scannedBarcode)) return;

            if (!_initialCache.TryGetValue(scannedBarcode, out var initial))
            {
                _isShowingAlert = true;
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Application.Current.MainPage.DisplayAlert("Error", $"Product '{scannedBarcode}' does not exist.", "OK"));
                _isShowingAlert = false;
                return;
            }

            if (_scannedCache.TryGetValue(scannedBarcode, out var scanned))
            {
                scanned.Quantity++;
                scanned.UpdatedAt = DateTime.Now;
            }
            else
            {
                scanned = new ScannedProduct
                {
                    Barcode = initial.Barcode,
                    Quantity = 1,
                    InitialQuantity = initial.Quantity,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _scannedCache[scannedBarcode] = scanned;
            }

            var existing = Products.FirstOrDefault(p => p.Barcode == scannedBarcode);
            if (existing != null)
            {
                existing.Quantity = scanned.Quantity;
                existing.UpdatedAt = scanned.UpdatedAt;
                Products.Move(Products.IndexOf(existing), 0);
            }
            else
            {
                Products.Insert(0, scanned);
            }

            lock (_saveLock) _savePending = true;
        }

        // ====== Background save loop ======
        private async Task SaveLoopAsync()
        {
            while (true)
            {
                await Task.Delay(5000);

                bool doSave;
                lock (_saveLock)
                {
                    doSave = _savePending;
                    _savePending = false;
                }

                if (!doSave) continue;

                try
                {
                    SaveCachesToDb();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DOTNET] SaveLoop ERROR: {ex}");
                }
            }
        }

        private void SaveCachesToDb()
        {
            using var tx = _conn.BeginTransaction();

            foreach (var s in _scannedCache.Values)
            {
                // Upsert ScannedProducts
                using var upsertCmd = _conn.CreateCommand();
                upsertCmd.CommandText = @"
            INSERT INTO ScannedProducts (Id, Barcode, Quantity, InitialQuantity, CreatedAt, UpdatedAt)
            VALUES ($id,$barcode,$qty,$init,$created,$updated)
            ON CONFLICT(Barcode) DO UPDATE SET
                Quantity=$qty,
                UpdatedAt=$updated";
                upsertCmd.Parameters.AddWithValue("$id", s.Id);
                upsertCmd.Parameters.AddWithValue("$barcode", s.Barcode);
                upsertCmd.Parameters.AddWithValue("$qty", s.Quantity);
                upsertCmd.Parameters.AddWithValue("$init", s.InitialQuantity);
                upsertCmd.Parameters.AddWithValue("$created", s.CreatedAt.ToString("o"));
                upsertCmd.Parameters.AddWithValue("$updated", s.UpdatedAt.ToString("o"));
                upsertCmd.ExecuteNonQuery();

                // Insert ScanLog
                using var logCmd = _conn.CreateCommand();
                logCmd.CommandText = @"
            INSERT INTO ScanLogs (Barcode, Quantity, InitialQuantity, Timestamp)
            VALUES ($barcode,$qty,$init,$ts)";
                logCmd.Parameters.AddWithValue("$barcode", s.Barcode);
                logCmd.Parameters.AddWithValue("$qty", s.Quantity);
                logCmd.Parameters.AddWithValue("$init", s.InitialQuantity);
                logCmd.Parameters.AddWithValue("$ts", DateTime.Now.ToString("o"));
                logCmd.ExecuteNonQuery();

                // 🔔 Tell LogsViewModel immediately
                WeakReferenceMessenger.Default.Send(
                    new NewScanLogMessage(new ScanLog
                    {
                        Barcode = s.Barcode,
                        Quantity = s.Quantity,
                        InitialQuantity = s.InitialQuantity,
                        Timestamp = DateTime.Now
                    }));
            }

            tx.Commit();
            Console.WriteLine("[DOTNET] Background save completed.");
        }


        // ====== Import & navigation ======
        private async Task OnImportExcelAsync()
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Excel File",
                FileTypes = FileTypes.Excel
            });
            if (result == null) return;

            await _importService.ImportExcelAsync(result.FullPath);

            BuildInitialCache();
            LoadProducts();
        }

        private async Task NavigateToDetailsAsync(ScannedProduct product)
        {
            if (product == null) return;
            _isNavigating = true;
            try
            {
                await Shell.Current.GoToAsync(
                    $"{nameof(DetailsPage)}?Barcode={product.Barcode}&Quantity={product.Quantity}&InitialQuantity={product.InitialQuantity}");
            }
            finally
            {
                _isNavigating = false;
                SelectedProduct = null;
            }
        }

        private async Task OnItemTappedAsync(ScannedProduct product)
        {
            if (product == null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(DetailsPage)}?Barcode={product.Barcode}&Quantity={product.Quantity}&InitialQuantity={product.InitialQuantity}");
        }

        private async Task OnGoToLogsAsync()
        {
            await Shell.Current.GoToAsync(nameof(LogsPage));
        }

        // ====== INotifyPropertyChanged ======
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
