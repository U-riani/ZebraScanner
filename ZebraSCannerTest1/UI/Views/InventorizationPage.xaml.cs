using Microsoft.Maui.ApplicationModel;
using ZebraSCannerTest1.UI.ViewModels;

namespace ZebraSCannerTest1.UI.Views;

public partial class InventorizationPage : ContentPage
{
    private readonly InventorizationViewModel _viewModel;

    public InventorizationPage(InventorizationViewModel vm)
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
            _viewModel.CurrentBarcode = scannedData;

            // ✅ Use the command instead of calling the method directly
            await _viewModel.AddProductCommand.ExecuteAsync(scannedData);

            barcodeEntry.Text = string.Empty;
            FocusScannerEntry();
        }
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();

        //var readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
        //var writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();

        //var mediaStatus = await Permissions.RequestAsync<Permissions.Media>();


        //if (readStatus != PermissionStatus.Granted ||
        //    writeStatus != PermissionStatus.Granted ||
        //    mediaStatus != PermissionStatus.Granted)

        //{
        //    await DisplayAlert("Permission needed", "Storage access is required to export Excel files.", "OK");
        //}

        FocusScannerEntry();

        await _viewModel.InitializeAsync();
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