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
    private CancellationTokenSource? _loadCts;

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
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Console.WriteLine("? XAML load error: " + ex);
            Shell.Current.DisplayAlert("XAML Error", ex.Message, "OK");
        }
        BindingContext = _vm = vm;
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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _vm.IsLoading = true;

        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        // ? Fire and forget background task
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(120, token); // let page animation finish
                if (token.IsCancellationRequested) return;

                if (!string.IsNullOrEmpty(_vm.ProductBarcode))
                {
                    // Heavy DB call
                    await _vm.LoadProductAsync();
                    if (token.IsCancellationRequested) return;

                    // Light UI update (load logs)
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _vm.LoadLogsCommand.ExecuteAsync(null);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // ignore — user navigated back early
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Shell.Current.DisplayAlert("Error", ex.Message, "OK"));
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _vm.IsLoading = false;
                });
            }
        }, token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _loadCts?.Cancel(); // ? cancels cleanly when navigating back
    }


}