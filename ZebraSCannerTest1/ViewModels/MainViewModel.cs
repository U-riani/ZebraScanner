using CommunityToolkit.Mvvm.Input;
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


namespace ZebraSCannerTest1.ViewModels
{


    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _db;
        private string _currentBarcode;
        private string _showCurrentBarcode;
        private ScannedProduct _selectedProduct;
        private bool _isNavigating = false;

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

        public MainViewModel(AppDbContext db)
        {
            _db = db;
            LoadProducts();

            AddProductCommand = new RelayCommand<string>(AddProduct);
            GoToDetailsCommand = new AsyncRelayCommand<ScannedProduct>(OnItemTappedAsync);

            WeakReferenceMessenger.Default.Register<ProductUpdatedMessage>(this, (r, m) =>
            {
                var existing = Products.FirstOrDefault(p => p.Id == m.Product.Id);
                if (existing != null)
                {
                    // Update existing product in the ObservableCollection
                    existing.Name = m.Product.Name;
                    existing.Quantity = m.Product.Quantity;
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


        public void LoadProducts()
        {
            Products.Clear();
            foreach (var scanned in  _db.ScannedProducts.OrderByDescending(p => p.Id))
            {
                Products.Add(new ScannedProduct
                {
                    Id = scanned.Id,
                    Name = scanned.Name,
                    Quantity = scanned.Quantity
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


        public void AddProduct(string scannedName)
        {
            // Check if the scanned barcode exists in InitialProducts
            var initial = _db.InitialProducts.FirstOrDefault(p => p.Name == scannedName);
            if (initial == null)
            {
                // Product doesn't exist in initial data — ignore or show alert
                // You can display a message to the user here
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current.MainPage.DisplayAlert("Error", $"Product '{scannedName}' does not exist.", "OK");
                });
                return;
            }

            // Check if product already exists in ScannedProducts
            var scanned = _db.ScannedProducts.FirstOrDefault(p => p.Name == scannedName);
            if (scanned != null)
            {
                // Increment quantity if already scanned
                scanned.Quantity += 1;
                _db.SaveChanges();

                var existingInCollection = Products.FirstOrDefault(p => p.Id == scanned.Id);
                if (existingInCollection != null)
                    existingInCollection.Quantity = scanned.Quantity;

                return;
            }

            // Add new scanned product
            var newScanned = new ScannedProduct
            {
                Name = initial.Name,
                Quantity = 1 // start with 1 for first scan
            };

            _db.ScannedProducts.Add(newScanned);
            _db.SaveChanges();

            Products.Insert(0, new ScannedProduct
            {
                Id = newScanned.Id,
                Name = newScanned.Name,
                Quantity = newScanned.Quantity
            });
        }


        private async void GoToDetailsAsync(ScannedProduct product)
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

        // Boilerplate for property change notifications
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
