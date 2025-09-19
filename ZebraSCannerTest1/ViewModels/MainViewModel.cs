using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections;
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




namespace ZebraSCannerTest1.ViewModels
{


    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _db;
        private readonly ExcelImportService _importService;
        private string _currentBarcode;
        private string _showCurrentBarcode;
        private ScannedProduct _selectedProduct;
        private bool _isNavigating = false;
        private bool _isShowingAlert = false;
        private bool _isManualEntryVisible = true;


        public IAsyncRelayCommand ImportExcelCommand { get; }

        public IAsyncRelayCommand<ScannedProduct> ItemTappedCommand { get; }
        // Add this in your ViewModel
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
                        // Fire-and-forget async navigation
                        _ = NavigateToDetailsAsync(_selectedProduct);

                        // Clear selection so same item can be tapped again later
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
            if (product == null)
                return;

            _isNavigating = true; // block further taps
            try
            {
                await Shell.Current.GoToAsync($"{nameof(Views.DetailsPage)}?ProductId={product.Id}");
            }
            finally
            {
                _isNavigating = false; // re-enable taps
                SelectedProduct = null; // clear selection
            }
        }
        public string CurrentBarcode
        {
            get => _currentBarcode;
            set
            {
                _currentBarcode = value;
                OnPropertyChanged(nameof(CurrentBarcode));
            }
        }


        public string ShowCurrentBarcode
        {
            get => _showCurrentBarcode;
            set
            {
                _showCurrentBarcode = value;
                OnPropertyChanged(nameof(ShowCurrentBarcode));
            }
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
                    // Update existing product in the ObservableCollection
                    existing.Barcode = m.Product.Barcode;
                    existing.Quantity = m.Product.Quantity;
                    existing.UpdatedAt = m.Product.UpdatedAt;
                    // Move updated product to top
                    Products.Move(Products.IndexOf(existing), 0);
                }
                else
                {
                    // Add new product if not exists
                    Products.Insert(0, m.Product);
                }
            });

        }



        //public void LoadProducts()
        //{
        //    Products.Clear();
        //    foreach (var product in _db.Products.OrderByDescending(p => p.Id))
        //    {
        //        Products.Add(product);
        //    }
        //}

        //public void AddProduct(string name)
        //{
        //   var newProduct = new TestModel { Name = name };
        //    _db.Products.Add(newProduct);
        //    _db.SaveChanges();
        //    Products.Insert(0, newProduct);
        //}
        private async Task OnImportExcelAsync()
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Excel File",
                    FileTypes = FileTypes.Excel
                });

                if (result != null)
                {
                    await _importService.ImportExcelAsync(result.FullPath);

                    LoadProducts();

                    await Application.Current.MainPage.DisplayAlert("Success", "Database imported successfully!", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }


        public void LoadProducts()
        {
            Products.Clear();
            var scannedProducts = _db.ScannedProducts
                .OrderByDescending(p => p.UpdatedAt)
                .ToList();
            foreach (var scanned in scannedProducts)
            {
                var initial = _db.InitialProducts.FirstOrDefault(p => p.Barcode == scanned.Barcode);

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

        //public void AddProduct(string name)
        //{
        //    // Check if product already exists in scanned table
        //    var existing = _db.ScannedProducts.FirstOrDefault(p => p.Name == name);
        //    if (existing != null)
        //    {
        //        existing.Quantity += 1;
        //        _db.SaveChanges();

        //        var existingInCollection = Products.FirstOrDefault(p => p.Id == existing.Id);
        //        if (existingInCollection != null)
        //            existingInCollection.Quantity = existing.Quantity;

        //        return;
        //    }

        //    // Get initial data from reference table
        //    var initial = _db.InitialProducts.FirstOrDefault(p => p.Name == name);

        //    var newScanned = new ScannedProduct
        //    {
        //        Name = initial?.Name ?? name,
        //        Quantity = initial?.Quantity ?? 0
        //    };

        //    _db.ScannedProducts.Add(newScanned);
        //    _db.SaveChanges();

        //    Products.Insert(0, new ScannedProduct
        //    {
        //        Id = newScanned.Id,
        //        Name = newScanned.Name,
        //        Quantity = newScanned.Quantity
        //    });
        //}


        public async Task AddProductAsync(string scannedBarcode)
        {
            if (_isShowingAlert)
                return;

            // Check if the scanned barcode exists in InitialProducts
            var initial = _db.InitialProducts.FirstOrDefault(p => p.Barcode == scannedBarcode);
            if (initial == null)
            {
                _isShowingAlert = true;
                // Await the alert to pause execution until user presses OK
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        $"Product '{scannedBarcode}' does not exist.",
                        "OK"
                    );
                });

                _isShowingAlert = false;
                return; // stop further scanning
            }

            // Check if product already exists in ScannedProducts
            var scanned = _db.ScannedProducts.FirstOrDefault(p => p.Barcode == scannedBarcode);
            if (scanned != null)
            {
                // Increment quantity if already scanned
                scanned.Quantity += 1;
                scanned.UpdatedAt = DateTime.Now;

                _db.SaveChanges();

                // Save a log for this scan
                _db.ScanLogs.Add(new ScanLog
                {
                    Barcode = scanned.Barcode,
                    Quantity = scanned.Quantity,
                    InitialQuantity = scanned.InitialQuantity,
                    ScannedProductId = scanned.Id,
                    Timestamp = DateTime.Now
                });
                _db.SaveChanges();

                // Update ObservableCollection
                var existingInCollection = Products.FirstOrDefault(p => p.Barcode == scanned.Barcode);
                if (existingInCollection != null)
                {
                    existingInCollection.Quantity = scanned.Quantity;
                    Products.Move(Products.IndexOf(existingInCollection), 0);

                }

                return;
            }

            // Add new scanned product
            var newScanned = new ScannedProduct
            {
                Barcode = initial.Barcode,
                Quantity = 1, // start with 1 for first scan
                InitialQuantity = initial.Quantity
            };

            _db.ScannedProducts.Add(newScanned);
            _db.SaveChanges();

            // ✅ Save log for first scan
            _db.ScanLogs.Add(new ScanLog
            {
                Barcode = newScanned.Barcode,
                Quantity = newScanned.Quantity,
                InitialQuantity = newScanned.InitialQuantity, // take initial quantity from ScannedProduct
                ScannedProductId = newScanned.Id,
                Timestamp = DateTime.Now
            });
            _db.SaveChanges();



            Products.Insert(0, new ScannedProduct
            {
                Id = newScanned.Id,
                Barcode = newScanned.Barcode,
                Quantity = newScanned.Quantity,
                InitialQuantity = newScanned.InitialQuantity
            });
        }


        private async Task GoToDetailsAsync(ScannedProduct product)
        {
            if (product == null)
                return;

            var navigationParameter = new Dictionary<string, object>
    {
        { "ProductId", product.Id }
    };

            await Shell.Current.GoToAsync(nameof(DetailsPage), true, navigationParameter);
        }


        private void OnGoToDetails(IList selection)
        {
            if (selection?.Count > 0 && selection[0] is ScannedProduct selectedProduct)
            {
                // Navigate to details page with selectedProduct
            }
        }

        private async Task OnItemTappedAsync(ScannedProduct product)
        {
            if (product == null)
                return;

            // Navigate to DetailsPage using Shell routing
            await Shell.Current.GoToAsync($"{nameof(Views.DetailsPage)}?ProductId={product.Id}");
        }
        private async Task OnGoToLogsAsync()
        {
            await Shell.Current.GoToAsync(nameof(LogsPage));
        }

        // Boilerplate for property change notifications
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
