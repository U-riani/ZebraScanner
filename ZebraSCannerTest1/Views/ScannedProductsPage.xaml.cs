using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1.Views;

public partial class ScannedProductsPage : ContentPage
{

    public ScannedProductsPage(ScannedProductsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
       
    }
}
