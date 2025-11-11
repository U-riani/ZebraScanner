using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class InventorizationMenuPage : ContentPage
{
    public InventorizationMenuPage(InventorizationMenuViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
