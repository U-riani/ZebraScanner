using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1.Views;

[QueryProperty(nameof(Barcode), "Barcode")]
[QueryProperty(nameof(Quantity), "Quantity")]
[QueryProperty(nameof(InitialQuantity), "InitialQuantity")]
[QueryProperty(nameof(Name), "Name")]
[QueryProperty(nameof(Color), "Color")]
[QueryProperty(nameof(Size), "Size")]
[QueryProperty(nameof(Price), "Price")]
[QueryProperty(nameof(ArticCode), "ArticCode")]
public partial class DetailsPage : ContentPage
{
    private readonly DetailsViewModel _vm;

    // Required fields
    public string Barcode { set => _vm.ProductBarcode = value; }
    public int Quantity { set => _vm.ScannedQuantity = value; }
    public int InitialQuantity { set => _vm.InitialQuantity = value; }

    // New static product info
    public string Name { set => _vm.ProductName = value; }
    public string Color { set => _vm.ProductColor = value; }
    public string Size { set => _vm.ProductSize = value; }
    public decimal Price { set => _vm.ProductPrice = value; }
    public string ArticCode { set => _vm.ProductArticCode = value; }

    public DetailsPage(DetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DetailsViewModel vm)
        {
            vm.LoadProductAsync().Wait(); // loads product details
            vm.LoadLogsCommand.Execute(null); // loads logs

        }
    }
}
