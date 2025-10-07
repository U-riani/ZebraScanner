using Microsoft.Maui.Controls;
using ZebraSCannerTest1.ViewModels;
using System;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Views
{
    public partial class ScannedProductsPage : ContentPage
    {
        private readonly ScannedProductsViewModel _vm;

        public ScannedProductsPage(ScannedProductsViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = vm;

        }

        private async void OnManualFilterClicked(object sender, EventArgs e)
        {
            // ?? Create and show popup
            var popup = new ManualFilterPopup();
            await Navigation.PushModalAsync(popup);

            // Wait for user result
            var result = await popup.Result;

            // ?? Apply filter if provided
            if (!string.IsNullOrWhiteSpace(result))
            {
                _vm.ApplyManualFilter(result);
                await DisplayAlert("? Manual Filter Applied", result, "OK");
            }
            else
            {
                await DisplayAlert("?? Cancelled", "No filter applied.", "OK");
            }
        }
    }
}
