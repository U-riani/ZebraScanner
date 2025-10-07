using CommunityToolkit.Maui.Views;
using System.Threading.Tasks;
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
[QueryProperty(nameof(IsReadOnly), "IsReadOnly")]

public partial class DetailsPage : ContentPage
{
    private readonly DetailsViewModel _vm;
    private bool _checkingUnsaved = false;

    public bool IsReadOnly
    {
        set => _vm.IsReadOnly = value;
    }

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
            await vm.LoadProductAsync(); // ? async-safe
            await vm.LoadLogsCommand.ExecuteAsync(null); // ? same async pattern
        }
    }


    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_vm.HasUnsavedChanges)
            {
                bool stay = await Shell.Current.DisplayAlert(
                    "Unsaved Changes",
                    "You have unsaved changes.\n\nPress 'Save' to keep your edits, or 'Leave' to discard.",
                    "Stay", "Leave");

                if (!stay)
                {
                    _vm.HasUnsavedChanges = false;
                    await Shell.Current.DisplayAlert("Changes Discarded", "Your edits were not saved.", "OK");
                }

                // ?? Don’t navigate automatically in any case
                return;
            }

            // ? No unsaved changes ? normal back
            await Shell.Current.GoToAsync("..");
        });

        return true; // block default back behavior
    }


}