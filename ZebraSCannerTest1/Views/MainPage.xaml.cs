using ZebraSCannerTest1.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace ZebraSCannerTest1.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;

        // Focus entry when page loads
        barcodeEntry.Loaded += (s, e) => FocusScannerEntry();

        // Scanner completes input
        barcodeEntry.Completed += BarcodeEntry_Completed;
    }

    private async void BarcodeEntry_Completed(object sender, EventArgs e)
    {
        var scannedData = barcodeEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(scannedData))
        {
            _viewModel.ShowCurrentBarcode = scannedData;
            await _viewModel.AddProductAsync(scannedData);
            barcodeEntry.Text = string.Empty;

            FocusScannerEntry();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
        var writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();

        var mediaStatus = await Permissions.RequestAsync<Permissions.Media>();


        if (readStatus != PermissionStatus.Granted ||
            writeStatus != PermissionStatus.Granted ||
            mediaStatus != PermissionStatus.Granted)

        {
            await DisplayAlert("Permission needed", "Storage access is required to export Excel files.", "OK");
        }

        FocusScannerEntry();
    }

    /// <summary>
    /// Forcefully resets and refocuses the scanner entry
    /// Fixes bug where Entry shows focus but does not accept input after navigation
    /// </summary>
    private void FocusScannerEntry()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Reset enabled state to forcefully refresh focus
            barcodeEntry.IsEnabled = false;
            barcodeEntry.IsEnabled = true;

            barcodeEntry.Focus();
        });
    }
}