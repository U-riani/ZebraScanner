using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class SalesMenuPage : ContentPage
{
	public SalesMenuPage(SalesMenuViewModel vm)
	{
		InitializeComponent();

		BindingContext = vm;
	}
}