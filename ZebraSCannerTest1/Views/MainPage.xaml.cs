using ZebraSCannerTest1.ViewModels;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;

        // Focus entry when page loads
        barcodeEntry.Loaded += (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() => barcodeEntry.Focus());
        };

        //// Scanner completes input (Enter key)
        //barcodeEntry.Completed += BarcodeEntry_Completed;

        //// 🔥 Subscribe to scroll-to-product event
        //_viewModel.NewProductAdded += product =>
        //{
        //    MainThread.BeginInvokeOnMainThread(() =>
        //    {
        //        if (scannedBarcodesCollectionView.ItemsSource != null)
        //        {
        //            scannedBarcodesCollectionView.ScrollTo(
        //                product,
        //                position: ScrollToPosition.Center,
        //                animate: true
        //            );
        //        }
        //    });
        //};

        // Subscribe to scroll event
        _viewModel.NewProductAdded += (p) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                scannedBarcodesCollectionView.ScrollTo(p, position: ScrollToPosition.MakeVisible, animate: false);
            });
        };

        // Scanner completes input
        barcodeEntry.Completed += BarcodeEntry_Completed;
    }

    private async void BarcodeEntry_Completed(object sender, EventArgs e)
    {
        var scannedData = barcodeEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(scannedData))
        {
            // Update label BEFORE adding
            _viewModel.ShowCurrentBarcode = scannedData;

            await _viewModel.AddProductAsync(scannedData);
            barcodeEntry.Text = string.Empty;

            MainThread.BeginInvokeOnMainThread(() => barcodeEntry.Focus());
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MainThread.BeginInvokeOnMainThread(() => barcodeEntry.Focus());
    }
}
