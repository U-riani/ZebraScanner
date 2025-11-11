using CommunityToolkit.Maui.Views;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

[QueryProperty(nameof(Barcode), "Barcode")]
[QueryProperty(nameof(Quantity), "Quantity")]
[QueryProperty(nameof(InitialQuantity), "InitialQuantity")]
[QueryProperty(nameof(Name), "Name")]
[QueryProperty(nameof(Color), "Color")]
[QueryProperty(nameof(Size), "Size")]
[QueryProperty(nameof(Price), "Price")]
[QueryProperty(nameof(ArticCode), "ArticCode")]
[QueryProperty(nameof(IsReadOnly), "IsReadOnly")]
[QueryProperty(nameof(Mode), "Mode")]
[QueryProperty(nameof(BoxId), "BoxId")]
public partial class DetailsPage : ContentPage
{
    private readonly DetailsViewModel _vm;
    private CancellationTokenSource? _loadCts;

    public InventoryMode Mode { set => _vm.CurrentMode = value; }
    public string BoxId { set => _vm.BoxId = value; }

    public bool IsReadOnly { set => _vm.IsReadOnly = value; }

    public string Barcode { set => _vm.ProductBarcode = value; }
    public int Quantity { set => _vm.ScannedQuantity = value; }
    public int InitialQuantity { set => _vm.InitialQuantity = value; }

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

    // --- Handle Android hardware back ---
    protected override bool OnBackButtonPressed()
    {
        if (_vm.HasUnsavedChanges)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool leave = await Shell.Current.DisplayAlert(
                    "Unsaved Changes",
                    "You have unsaved changes.\n\nDo you want to leave without saving?",
                    "Leave", "Stay");

                if (leave)
                {
                    _vm.HasUnsavedChanges = false;
                    await Shell.Current.GoToAsync("..");
                }
            });

            return true; // Block until user decides
        }

        return base.OnBackButtonPressed();
    }

    // --- Catch Shell navigation (toolbar / swipe) ---
    protected override void OnAppearing()
    {
        base.OnAppearing();

        Shell.Current.Navigating += OnShellNavigating;

        _vm.IsLoading = true;
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(120, token);
                if (token.IsCancellationRequested) return;

                if (!string.IsNullOrEmpty(_vm.ProductBarcode))
                {
                    _vm.LoadProductAsync();
                    if (token.IsCancellationRequested) return;

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await _vm.LoadLogsCommand.ExecuteAsync(null));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Shell.Current.DisplayAlert("Error", ex.Message, "OK"));
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => _vm.IsLoading = false);
            }
        }, token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Shell.Current.Navigating -= OnShellNavigating;
        _loadCts?.Cancel();
    }

    private void OnShellNavigating(object sender, ShellNavigatingEventArgs e)
    {
        if (_vm.HasUnsavedChanges)
        {
            e.Cancel(); // Block Shell navigation
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool leave = await Shell.Current.DisplayAlert(
                    "Unsaved Changes",
                    "You have unsaved changes.\n\nDo you want to leave without saving?",
                    "Leave", "Stay");

                if (leave)
                {
                    _vm.HasUnsavedChanges = false;
                    Shell.Current.Navigating -= OnShellNavigating;
                    await Shell.Current.GoToAsync("..");
                }
            });
        }
    }
}
