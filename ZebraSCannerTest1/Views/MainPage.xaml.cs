using ZebraSCannerTest1.ViewModels;

namespace ZebraSCannerTest1;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel; // connect ViewModel to XAML

        //barcodeEntry.TextChanged += (s, args) =>
        {                   
            //barcodeEntry.Text = string.Empty;

            var scannedData = _viewModel.CurrentBarcode;
            if (!string.IsNullOrWhiteSpace(scannedData))
            {
                _viewModel.ShowCurrentBarcode = scannedData;
                _viewModel.AddProductAsync(scannedData);
                _viewModel.CurrentBarcode = string.Empty; // clear Entry

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if(_viewModel.Products.Count > 0)
                    {
                        scannedBarcodesCollectionView.ScrollTo(
                            _viewModel.Products[0], 
                            position: ScrollToPosition.Start, 
                            animate: true
                        );
                    }
                });
            }
        };

        barcodeEntry.Loaded += (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() => barcodeEntry.Focus());
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        barcodeEntry.TextChanged -= BarcodeEntry_TextChanged; // avoid double subscriptions
        barcodeEntry.TextChanged += BarcodeEntry_TextChanged;

        MainThread.BeginInvokeOnMainThread(() => barcodeEntry.Focus());
    }

    private async void BarcodeEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        var scannedData = e.NewTextValue; // use the Entry's new value
        if (!string.IsNullOrWhiteSpace(scannedData))
        {
            _viewModel.ShowCurrentBarcode = scannedData;
            await _viewModel.AddProductAsync(scannedData);

            barcodeEntry.Text = string.Empty; // reset Entry

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_viewModel.Products.Count > 0)
                {
                    scannedBarcodesCollectionView.ScrollTo(
                        _viewModel.Products[0],
                        position: ScrollToPosition.Start,
                        animate: true
                    );
                }
            });
        }
    }

    private async void OnStartScannerClicked(object sender, EventArgs e)
    {
        // For now, simulate adding a scanned barcode
        string scannedData = _viewModel.CurrentBarcode;
        if (!string.IsNullOrWhiteSpace(scannedData))
        {
            await _viewModel.AddProductAsync(scannedData);
            _viewModel.CurrentBarcode = string.Empty; // clears Entry

        }

    }
}
