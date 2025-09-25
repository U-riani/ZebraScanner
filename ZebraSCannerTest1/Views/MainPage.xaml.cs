using ZebraSCannerTest1.ViewModels;

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

        // Scanner completes input (Enter key)
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

            MainThread.BeginInvokeOnMainThread(() =>
            {
                barcodeEntry.Focus();

                if (_viewModel.Products.Count > 0)
                {
                    scannedBarcodesCollectionView.ScrollTo(
                        _viewModel.Products[0],
                        position: ScrollToPosition.Start,
                        animate: false
                    );
                }
            });
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MainThread.BeginInvokeOnMainThread(() => barcodeEntry.Focus());
    }
}
