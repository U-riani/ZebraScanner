using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Infrastructure.Repositories;
using ZebraSCannerTest1.Messages;


namespace ZebraSCannerTest1.UI.ViewModels
{
    public partial class SalesViewModel : BaseViewModel
    {
        private readonly SalesRepository _repo;
        public SalesViewModel(SalesRepository repo)
        {
            _repo = repo;

        }

        [ObservableProperty] string barcode;
        [ObservableProperty] string lastScannedBarcode;

        [ObservableProperty] string name;
        [ObservableProperty] string color;
        [ObservableProperty] string size;
        [ObservableProperty] string saleType;
        [ObservableProperty] string oldPrice;
        [ObservableProperty] string newPrice;
        [ObservableProperty] string articCode;

        [RelayCommand]
        async Task BarcodeCompleted(string scannedValue)
        {
            LastScannedBarcode = scannedValue;
            
            var record = await _repo.GetSaleAsync(scannedValue);

            if (record != null)
            {
                Name = record.Name;
                Color = record.Color;
                Size = record.Size;
                SaleType = record.SaleType;
                OldPrice = record.OldPrice;
                NewPrice = record.NewPrice;
                ArticCode = record.ArticCode;
            }else
            {
                Name = string.Empty;
                Color = string.Empty;
                Size = string.Empty;
                SaleType = string.Empty;
                OldPrice = string.Empty;
                NewPrice = string.Empty;
                ArticCode = string.Empty;

                await Shell.Current.DisplayAlert("Not Found", "No sale record exists for this barcode.", "OK");
            }

            Barcode = "";
        }


    }
}
