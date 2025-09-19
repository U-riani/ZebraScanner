using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;
using ZebraSCannerTest1.Views;
using ZebraSCannerTest1.Services;
using Microsoft.Maui.Storage;
using ZebraSCannerTest1.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ZebraSCannerTest1.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _db;
        private readonly ExcelImportService _importService;
        private Dictionary<string, InitialProduct> _initialCache = new();

        private string _currentBarcode;
        private string _showCurrentBarcode;
        private ScannedProduct _selectedProduct;
        private bool _isNavigating = false;
        private bool _isShowingAlert = false;
        private bool _isManualEntryVisible = true;

        public IAsyncRelayCommand ImportExcelCommand { get; }
        public IAsyncRelayCommand<ScannedProduct> ItemTappedCommand { get; }
        public ObservableCollection<ScannedProduct> ScanHistory { get; set; } = new();

        public ScannedProduct SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (_selectedProduct != value)
                {
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
        }

        public bool IsManualEntryVisible
        {
            get => _isManualEntryVisible;
            set
            {
                if (_isManualEntryVisible != value)
                {
                    _isManualEntryVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ToggleManualEntryCommand { get; }

        private async Task NavigateToDetailsAsync(ScannedProduct product)
        {
            if (product == null) return;

            _isNavigating = true;
            try
            {
                await Shell.Current.GoToAsync($"{nameof(Views.DetailsPage)}?ProductId={product.Id}");
            }
            finally
            {
                _isNavigating = false;
                SelectedProduct = null;
            }
        }

        public string CurrentBarcode
        {
            get => _currentBarcode;
            set { _currentBarcode = value; OnPropertyChanged(nameof(CurrentBarcode)); }
        }

        public string ShowCurrentBarcode
        {
            get => _showCurrentBarcode;
            set { _showCurrentBarcode = value; OnPropertyChanged(nameof(ShowCurrentBarcode)); }
        }

        public ObservableCollection<ScannedProduct> Products { get; set; }
            = new ObservableCollection<ScannedProduct>();

        public ICommand AddProductCommand { get; }
        public ICommand GoToDetailsCommand { get; }
        public ICommand GoToLogsCommand { get; }

        public MainViewModel(AppDbContext db, ExcelImportService importService)
        {
            _db = db;
            _importService = importService;

            LoadProducts();
            BuildInitialCache();

            AddProductCommand = new AsyncRelayCommand<string>(AddProductAsync);
            GoToDetailsCommand = new AsyncRelayCommand<ScannedProduct>(OnItemTappedAsync);
            GoToLogsCommand = new AsyncRelayCommand(OnGoToLogsAsync);
            ImportExcelCommand = new AsyncRelayCommand(OnImportExcelAsync);

            ToggleManualEntryCommand = new RelayCommand(() =>
            {
                IsManualEntryVisible = !IsManualEntryVisible;
            });

            WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
            {
                var existing = Products.FirstOrDefault(p => p.Barcode == m.Product.Barcode);
                if (existing != null)
                {
                    existing.Barcode = m.Product.Barcode;
                    existing.Quantity = m.Product.Quantity;
                    existing.UpdatedAt = m.Product.UpdatedAt;
                    Products.Move(Products.IndexOf(existing), 0);
                }
                else
                {
                    Products.Insert(0, m.Product);
                }
            });
        }

        // 🔥 Cache builder
        private void BuildInitialCache()
        {
            _initialCache = _db.InitialProducts
                .AsNoTracking()
                .ToDictionary(p => p.Barcode, p => p);
            Console.WriteLine($"[DOTNET] Cache built with {_initialCache.Count} items");
        }

        private async Task OnImportExcelAsync()
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Excel File",
                    FileTypes = FileTypes.Excel
                });

                if (result == null)
                    return;

                string filePath = result.FullPath;
                Console.WriteLine($"[DOTNET] File picked: {filePath}");

                await _importService.ImportExcelAsync(filePath);

                // 🔄 Refresh cache + UI
                BuildInitialCache();
                LoadProducts();

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Success",
                        "Database imported successfully!",
                        "OK"
                    );
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DOTNET] Import ERROR: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        ex.Message,
                        "OK"
                    );
                });
            }
        }

        public void LoadProducts()
        {
            Products.Clear();
            var scannedProducts = _db.ScannedProducts
                .OrderByDescending(p => p.UpdatedAt)
                .AsNoTracking()
                .ToList();

            foreach (var scanned in scannedProducts)
            {
                _initialCache.TryGetValue(scanned.Barcode, out var initial);

                Products.Add(new ScannedProduct
                {
                    Id = scanned.Id,
                    Barcode = scanned.Barcode,
                    Quantity = scanned.Quantity,
                    InitialQuantity = initial?.Quantity ?? 0,
                    CreatedAt = scanned.CreatedAt,
                    UpdatedAt = scanned.UpdatedAt
                });
            }
        }

        public async Task AddProductAsync(string scannedBarcode)
        {
            if (_isShowingAlert) return;
            scannedBarcode = scannedBarcode?.Trim();

            Console.WriteLine($"[DOTNET] Scanned: '{scannedBarcode}' (len={scannedBarcode?.Length})");

            if (!_initialCache.TryGetValue(scannedBarcode, out var initial))
            {
                _isShowingAlert = true;
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        $"Product '{scannedBarcode}' does not exist.",
                        "OK"
                    );
                });
                _isShowingAlert = false;
                return;
            }

            // ✅ Use EF Core async methods, no Task.Run
            var scanned = await _db.ScannedProducts.FirstOrDefaultAsync(p => p.Barcode == scannedBarcode);
            if (scanned != null)
            {
                scanned.Quantity += 1;
                scanned.UpdatedAt = DateTime.Now;

                _db.ScanLogs.Add(new ScanLog
                {
                    Barcode = scanned.Barcode,
                    Quantity = scanned.Quantity,
                    InitialQuantity = scanned.InitialQuantity,
                    ScannedProductId = scanned.Id,
                    Timestamp = DateTime.Now
                });
            }
            else
            {
                var newScanned = new ScannedProduct
                {
                    Barcode = initial.Barcode,
                    Quantity = 1,
                    InitialQuantity = initial.Quantity
                };
                _db.ScannedProducts.Add(newScanned);
                await _db.SaveChangesAsync(); // save once to get Id

                _db.ScanLogs.Add(new ScanLog
                {
                    Barcode = newScanned.Barcode,
                    Quantity = newScanned.Quantity,
                    InitialQuantity = newScanned.InitialQuantity,
                    ScannedProductId = newScanned.Id,
                    Timestamp = DateTime.Now
                });
            }

            await _db.SaveChangesAsync();

            // ✅ Update UI after DB is done
            var existingInCollection = Products.FirstOrDefault(p => p.Barcode == scannedBarcode);
            if (existingInCollection != null)
            {
                existingInCollection.Quantity += 1;
                Products.Move(Products.IndexOf(existingInCollection), 0);
            }
            else
            {
                Products.Insert(0, new ScannedProduct
                {
                    Barcode = scannedBarcode,
                    Quantity = 1,
                    InitialQuantity = initial.Quantity
                });
            }
        }


        private async Task OnItemTappedAsync(ScannedProduct product)
        {
            if (product == null) return;
            await Shell.Current.GoToAsync($"{nameof(Views.DetailsPage)}?ProductId={product.Id}");
        }

        private async Task OnGoToLogsAsync()
        {
            await Shell.Current.GoToAsync(nameof(LogsPage));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
