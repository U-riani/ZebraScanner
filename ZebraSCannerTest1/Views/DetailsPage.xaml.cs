using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1.Views;

[QueryProperty(nameof(Barcode), "Barcode")]
[QueryProperty(nameof(Quantity), "Quantity")]
[QueryProperty(nameof(InitialQuantity), "InitialQuantity")]
public partial class DetailsPage : ContentPage
{
    private readonly DetailsViewModel _vm;

    public string Barcode { set => _vm.ProductBarcode = value; }
    public int Quantity { set => _vm.ScannedQuantity = value; }
    public int InitialQuantity { set => _vm.InitialQuantity = value; }

    public DetailsPage(DetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
