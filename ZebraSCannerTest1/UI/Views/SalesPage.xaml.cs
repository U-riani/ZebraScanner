using CommunityToolkit.Mvvm.Messaging;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class SalesPage : ContentPage
{

	public SalesPage(SalesViewModel vm)
	{
		InitializeComponent();
		BindingContext =  vm;

		Loaded += (s, e) => BarcodeEntry.Focus();

		WeakReferenceMessenger.Default.Register<BarcodeScannedMessage>(this, (r, m) =>
		{
			BarcodeEntry.Focus();
		});

	}

	
}