using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Helpers;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Services;
using ZebraSCannerTest1.Views;

namespace ZebraSCannerTest1.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _db;
        private readonly ExcelImportService _importService;

        // Fast lookup caches
        private Dictionary<string, InitialProduct> _initialCache = new();
        private Dictionary<string, ScannedProduct> _scannedCache = new();

        // UI state
        private string _currentBarcode;
        private string _showCurrentBarcode;
        private ScannedProduct _selectedProduct;
        private bool _isNavigating;
        private bool _isShowingAlert;
        private bool _isManualEntryVisible = true;

        // background save control
        private readonly object _saveLock = new();
        private bool _savePending;

        public ObservableCollection<ScannedProduct> Products { get; } = new();

        public MainViewModel(AppDbContext db, ExcelImportService importService)
        {
            _db = db;
            _importService = importService;

            LoadProducts();
            BuildInitialCache();
            BuildScannedCache();

            AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
            GoToDetailsCommand = new AsyncRelayCommand<ScannedProduct>(OnItemTappedAsync);
            GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
            ImportExcelCommand = new AsyncRelayCommand(OnImportExcelAsync);
            ToggleManualEntryCommand = new RelayCommand(() => IsManualEntryVisible = !IsManualEntryVisible);

            // background periodic save
            Task.Run(SaveLoopAsync);
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
            _initialCache = _db.InitialProducts
                .AsNoTracking()
                .ToDictionary(p => p.Barcode, p => p);
            Console.WriteLine($"[DOTNET] Initial cache size: {_initialCache.Count}");
        }

        private void BuildScannedCache()
        {
            _scannedCache = _db.ScannedProducts
                .AsNoTracking()
                .ToDictionary(p => p.Barcode, p => p);
            Console.WriteLine($"[DOTNET] Scanned cache size: {_scannedCache.Count}");
        }

        // ====== UI list load ======
        public void LoadProducts()
        {
            Products.Clear();
            var list = _db.ScannedProducts.AsNoTracking().OrderByDescending(p => p.UpdatedAt).ToList();
            foreach (var s in list)
            {
                _initialCache.TryGetValue(s.Barcode, out var init);
                Products.Add(new ScannedProduct
                {
                    Id = s.Id,
                    Barcode = s.Barcode,
                    Quantity = s.Quantity,
                    InitialQuantity = init?.Quantity ?? 0,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                });
            }
        }

        // ====== FAST scan path (no DB hit, only memory; DB saved in background) ======
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

            // Update the in-memory scanned cache
            if (_scannedCache.TryGetValue(scannedBarcode, out var scanned))
            {
                scanned.Quantity += 1;
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

            // Instant UI update
            var existing = Products.FirstOrDefault(p => p.Barcode == scannedBarcode);
            if (existing != null)
            {
                existing.Quantity = scanned.Quantity;
                existing.UpdatedAt = scanned.UpdatedAt;
                Products.Move(Products.IndexOf(existing), 0);
            }
            else
            {
                Products.Insert(0, new ScannedProduct
                {
                    Barcode = scanned.Barcode,
                    Quantity = scanned.Quantity,
                    InitialQuantity = scanned.InitialQuantity,
                    CreatedAt = scanned.CreatedAt,
                    UpdatedAt = scanned.UpdatedAt
                });
            }

            // flag background save
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
                    await SaveCachesToDbAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DOTNET] SaveLoop ERROR: {ex}");
                }
            }
        }

        private async Task SaveCachesToDbAsync()
        {
            var snapshot = _scannedCache.Values.Select(v => new
            {
                v.Barcode,
                v.Quantity,
                v.InitialQuantity,
                v.CreatedAt,
                v.UpdatedAt
            }).ToList();

            foreach (var s in snapshot)
            {
                var dbItem = await _db.ScannedProducts.FirstOrDefaultAsync(p => p.Barcode == s.Barcode);
                if (dbItem != null)
                {
                    dbItem.Quantity = s.Quantity;
                    dbItem.UpdatedAt = s.UpdatedAt;
                }
                else
                {
                    _db.ScannedProducts.Add(new ScannedProduct
                    {
                        Barcode = s.Barcode,
                        Quantity = s.Quantity,
                        InitialQuantity = s.InitialQuantity,
                        CreatedAt = s.CreatedAt,
                        UpdatedAt = s.UpdatedAt
                    });
                }

                _db.ScanLogs.Add(new ScanLog
                {
                    Barcode = s.Barcode,
                    Quantity = s.Quantity,
                    InitialQuantity = s.InitialQuantity,
                    Timestamp = DateTime.Now
                });
            }

            await _db.SaveChangesAsync();
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
                    $"{nameof(DetailsPage)}" +
                    $"?Barcode={product.Barcode}" +
                    $"&Quantity={product.Quantity}" +
                    $"&InitialQuantity={product.InitialQuantity}");
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
                $"{nameof(DetailsPage)}" +
                $"?Barcode={product.Barcode}" +
                $"&Quantity={product.Quantity}" +
                $"&InitialQuantity={product.InitialQuantity}");
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
