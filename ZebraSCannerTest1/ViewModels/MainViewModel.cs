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
        private TestModel _selectedProduct;
        private bool _isNavigating = false;

        public IAsyncRelayCommand<TestModel> ItemTappedCommand { get; }

        public TestModel SelectedProduct
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

        private async Task NavigateToDetailsAsync(TestModel product)
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


        public ObservableCollection<TestModel> Products { get; set; }
            = new ObservableCollection<TestModel>();

        public ICommand AddProductCommand { get; }
        public ICommand GoToDetailsCommand { get; }

        public MainViewModel(AppDbContext db)
        {
            _db = db;
            LoadProducts();

            AddProductCommand = new RelayCommand<string>(AddProduct);
            GoToDetailsCommand = new AsyncRelayCommand<TestModel>(OnItemTappedAsync);

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

        public void LoadProducts()
        {
            Products.Clear();
            foreach (var product in _db.Products.OrderByDescending(p => p.Id))
            {
                Products.Add(product);
            }
        }

        public void AddProduct(string name)
        {
           var newProduct = new TestModel { Name = name };
            _db.Products.Add(newProduct);
            _db.SaveChanges();
            Products.Insert(0, newProduct);
        }


        private async void GoToDetailsAsync(TestModel product)
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
            if (selection?.Count > 0 && selection[0] is TestModel selectedProduct)
            {
                // Navigate to details page with selectedProduct
            }
        }

        private async Task OnItemTappedAsync(TestModel product)
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
