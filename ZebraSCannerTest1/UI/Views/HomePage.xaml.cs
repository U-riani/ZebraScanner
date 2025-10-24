using ZebraSCannerTest1.UI.ViewModels;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.Views;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        BindingContext = new HomeViewModel();
    }
}
