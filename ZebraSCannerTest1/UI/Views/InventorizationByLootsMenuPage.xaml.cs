using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class InventorizationByLootsMenuPage : ContentPage
{
	public InventorizationByLootsMenuPage(InventorizationByLootsMenuViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
    }
}