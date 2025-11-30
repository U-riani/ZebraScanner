using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Core.Services;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.UI.Services;
using ZebraSCannerTest1.UI.Views;

namespace ZebraSCannerTest1.UI.ViewModels
{
    public partial class SalesMenuViewModel : BaseViewModel
    {
        public IRelayCommand NavigateToSalesPageCommand { get; }
        public IRelayCommand OnImportSalesDataCommand { get; }

        private readonly SalesExcelImportService _salesExcelImportService;
        private readonly PopupService _popup;


        public SalesMenuViewModel(SalesExcelImportService salesExcelImportService, PopupService popup)
        {
            _salesExcelImportService = salesExcelImportService;
            _popup = popup;

            NavigateToSalesPageCommand = new RelayCommand(async () =>
            {
                await Shell.Current.GoToAsync(nameof(SalesPage));
            });

            OnImportSalesDataCommand = new AsyncRelayCommand(OnImportSalesData);

        }

        public async Task OnImportSalesData()
        {
            var choice = await Shell.Current.DisplayActionSheet(
            $"Import Sales Data",
            "Cancel", null,
            "From Device", "From Server");

            if (choice == "Cancel" || string.IsNullOrWhiteSpace(choice))
                return;

            bool popupOpened = false;

            try
            {
                await _popup.ShowProgressAsync("Preparing import...");
                popupOpened = true;

                if (choice == "From Server")
                {
                    _popup.UpdateMessage("Not implemented yet...");
                    await Task.Delay(600);
                }
                else
                {

                    var result = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = $"Select File to Import for Sales",
                        FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.Android, new[] { "*/*" } },
                            { DevicePlatform.WinUI, new[] { ".xlsx", ".json", ".db" } }
                        })
                    });

                    if (result == null)
                        return;

                    using var stream = await result.OpenReadAsync();
                    string ext = Path.GetExtension(result.FileName).ToLowerInvariant();


                    switch (ext)
                    {
                        case ".xlsx":
                            await _salesExcelImportService.ImportSalesExcelAsync(stream, result.FileName);
                            _popup.UpdateMessage("Importing Excel data...");
                            break;
                        case ".json":
                            await Shell.Current.DisplayAlert(null, "Will be added soon..", "OK");
                            break;
                        case ".db":
                            await Shell.Current.DisplayAlert(null, "Will be added soon..", "OK");
                            break;
                        default:
                            throw new InvalidOperationException("Please select a valid .xlsx, .json, or .db file.");
                    }

                    _popup.UpdateMessage("✅ Import Complete (Device)");
                }
                await MainThread.InvokeOnMainThreadAsync(() => _popup.Close());
                popupOpened = false;

                await Shell.Current.DisplayAlert("Success", "Sales data imported successfully.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
